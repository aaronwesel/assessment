using Crawl.Ai.DeepSeek;
using Crawl.Ai.Models;

namespace Crawl.Ai.Summarization;

public interface IResultSummarizer
{
    Task<SummarizeResponse> SummarizeAsync(SummarizeRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summarizes crawl job results for operators. May cite precomputed Domain Link Ratio values
/// supplied by .NET, but must never compute them.
/// </summary>
public sealed class ResultSummarizer : IResultSummarizer
{
    private readonly IDeepSeekClient _deepSeek;

    public ResultSummarizer(IDeepSeekClient deepSeek)
    {
        _deepSeek = deepSeek;
    }

    public async Task<SummarizeResponse> SummarizeAsync(
        SummarizeRequest request,
        CancellationToken cancellationToken = default)
    {
        var local = LocalSummary(request);

        if (!_deepSeek.IsConfigured)
        {
            return local with { UsedModel = false, DegradedReason = AiDegradedCopy.OfflineHeuristicSummary };
        }

        var system = """
            Summarize a web crawl job for a medical billing / RCM operator.
            Cite page URLs. You may mention Domain Link Ratio values only if provided in the input —
            never recompute ratios or invent link graphs. Return plain text summary then a line
            'HIGHLIGHTS:' with bullet-like short lines.
            """;

        var pagesBlob = string.Join(
            "\n",
            request.Pages.Take(40).Select(p =>
                $"- {p.Url} depth={p.Depth} status={p.Status} dlr={(p.DomainLinkRatio?.ToString("0.###") ?? "n/a")} err={p.Error ?? ""}"));

        var user = $"""
            Job {request.JobId}
            Seed: {request.SeedUrl}
            Status: {request.Status}
            Pages: {request.PageCount}; Edges: {request.EdgeCount}
            Failures: {string.Join("; ", request.Failures ?? Array.Empty<string>())}
            Pages:
            {pagesBlob}
            """;

        var result = await _deepSeek.CompleteAsync(system, user, cancellationToken).ConfigureAwait(false);
        if (!result.UsedModel || string.IsNullOrWhiteSpace(result.Content))
        {
            return local with { UsedModel = false, DegradedReason = result.DegradedReason };
        }

        var (summary, highlights) = SplitHighlights(result.Content);
        return new SummarizeResponse(
            request.JobId,
            summary,
            highlights.Count > 0 ? highlights : local.Highlights,
            local.CitedUrls,
            true,
            null);
    }

    internal static SummarizeResponse LocalSummary(SummarizeRequest request)
    {
        var failures = request.Failures?.ToList() ?? request.Pages
            .Where(p => !string.IsNullOrEmpty(p.Error))
            .Select(p => $"{p.Url}: {p.Error}")
            .ToList();

        // Cite ratios provided by domain layer — do not compute.
        var lowRatio = request.Pages
            .Where(p => p.DomainLinkRatio is < 0.2)
            .Select(p => p.Url)
            .Take(5)
            .ToList();

        var highlights = new List<string>
        {
            $"Status {request.Status} with {request.PageCount} pages / {request.EdgeCount} edges.",
            failures.Count == 0 ? "No recorded page failures." : $"{failures.Count} failure(s) noted."
        };

        if (lowRatio.Count > 0)
        {
            highlights.Add($"Pages with provided Domain Link Ratio < 0.2: {string.Join(", ", lowRatio)}.");
        }

        var summary =
            $"Crawl of '{request.SeedUrl}' finished as {request.Status}. " +
            $"Captured {request.PageCount} pages and {request.EdgeCount} edges. " +
            (failures.Count > 0
                ? $"Failures include: {string.Join("; ", failures.Take(3))}."
                : "No failures recorded.");

        var cited = request.Pages.Select(p => p.Url).Distinct().Take(12).ToList();
        return new SummarizeResponse(request.JobId, summary, highlights, cited, false, null);
    }

    private static (string Summary, IReadOnlyList<string> Highlights) SplitHighlights(string content)
    {
        var idx = content.IndexOf("HIGHLIGHTS:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return (content.Trim(), Array.Empty<string>());
        }

        var summary = content[..idx].Trim();
        var rest = content[(idx + "HIGHLIGHTS:".Length)..];
        var highlights = rest
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimStart('-', '*', ' '))
            .Where(l => l.Length > 0)
            .Take(8)
            .ToList();
        return (summary, highlights);
    }
}
