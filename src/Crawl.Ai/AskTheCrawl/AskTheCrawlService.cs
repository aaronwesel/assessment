using System.Text.RegularExpressions;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.Models;

namespace Crawl.Ai.AskTheCrawl;

public interface IAskTheCrawlService
{
    Task<AskTheCrawlResponse> AskAsync(AskTheCrawlRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Chat over job results with citations. Filters on precomputed Domain Link Ratio from .NET
/// when the operator asks — never recomputes ratio or parses links from HTML.
/// </summary>
public sealed class AskTheCrawlService : IAskTheCrawlService
{
    private static readonly Regex RatioQuestion = new(
        @"ratio\s*(?:&lt;|<|less\s+than)\s*(0?\.\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IDeepSeekClient _deepSeek;

    public AskTheCrawlService(IDeepSeekClient deepSeek)
    {
        _deepSeek = deepSeek;
    }

    public async Task<AskTheCrawlResponse> AskAsync(
        AskTheCrawlRequest request,
        CancellationToken cancellationToken = default)
    {
        var local = LocalAnswer(request);

        if (!_deepSeek.IsConfigured)
        {
            return local with
            {
                UsedModel = false,
                DegradedReason = "DEEPSEEK_API_KEY not configured; heuristic answer only."
            };
        }

        var system = """
            Answer questions about a completed crawl job. Cite page URLs from the provided snapshots.
            Domain Link Ratio values are precomputed by the .NET crawl domain — use them as given;
            never recompute or invent link edges. If data is missing, say so.
            """;

        var pagesBlob = string.Join(
            "\n",
            request.Pages.Take(60).Select(p =>
                $"- id={p.PageId} url={p.Url} depth={p.Depth} dlr={(p.DomainLinkRatio?.ToString("0.###") ?? "n/a")} status={p.Status}"));

        var user = $"Question: {request.Question}\nPages:\n{pagesBlob}";
        var result = await _deepSeek.CompleteAsync(system, user, cancellationToken).ConfigureAwait(false);
        if (!result.UsedModel || string.IsNullOrWhiteSpace(result.Content))
        {
            return local with { UsedModel = false, DegradedReason = result.DegradedReason };
        }

        return new AskTheCrawlResponse(
            request.JobId,
            result.Content.Trim(),
            local.Citations,
            true,
            null);
    }

    internal static AskTheCrawlResponse LocalAnswer(AskTheCrawlRequest request)
    {
        var q = request.Question ?? string.Empty;
        var match = RatioQuestion.Match(q);
        if (match.Success && double.TryParse(match.Groups[1].Value, out var threshold))
        {
            // Filter using provided ratios only — AI does not compute Domain Link Ratio.
            var hits = request.Pages
                .Where(p => p.DomainLinkRatio is double d && d < threshold)
                .ToList();

            var citations = hits.Select(h => new AskTheCrawlCitation(
                h.Url,
                h.PageId,
                $"DomainLinkRatio={h.DomainLinkRatio:0.###} (provided by crawl domain)")).ToList();

            var answer = hits.Count == 0
                ? $"No pages in this job have a provided Domain Link Ratio < {threshold}."
                : $"{hits.Count} page(s) have provided Domain Link Ratio < {threshold}: "
                  + string.Join(", ", hits.Select(h => h.Url));

            return new AskTheCrawlResponse(request.JobId, answer, citations, false, null);
        }

        if (q.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || q.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            var fails = request.Pages.Where(p => !string.IsNullOrEmpty(p.Error)).ToList();
            var citations = fails.Select(f => new AskTheCrawlCitation(f.Url, f.PageId, f.Error!)).ToList();
            var answer = fails.Count == 0
                ? "No pages report errors in the provided snapshots."
                : $"{fails.Count} page(s) report errors.";
            return new AskTheCrawlResponse(request.JobId, answer, citations, false, null);
        }

        var sample = request.Pages.Take(5)
            .Select(p => new AskTheCrawlCitation(p.Url, p.PageId, $"depth={p.Depth}"))
            .ToList();
        return new AskTheCrawlResponse(
            request.JobId,
            $"Job has {request.Pages.Count} page snapshot(s). Ask about failures or Domain Link Ratio thresholds for a filtered answer.",
            sample,
            false,
            null);
    }
}
