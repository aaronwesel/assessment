using System.Text.Json;
using System.Text.RegularExpressions;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.Models;
using Crawl.Ai.Options;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.FieldFinder;

public interface IFieldFinderService
{
    Task<FieldFinderResponse> SuggestAsync(FieldFinderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Suggests extraction targets from page HTML/text for operator confirmation.
/// Does not parse crawl links or compute Domain Link Ratio.
/// </summary>
public sealed class FieldFinderService : IFieldFinderService
{
    private static readonly Regex InputRegex = new(
        @"<input\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrRegex = new(
        @"(\w+)\s*=\s*[""']([^""']*)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabelishRegex = new(
        @"<(?:label|th|td|span|div)[^>]{0,120}>\s*([^<]{2,80})\s*</",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] BillingHints =
    [
        "member", "subscriber", "eligibility", "claim", "npi", "payer", "policy",
        "group", "dob", "date of birth", "patient", "diagnosis", "cpt", "icd"
    ];

    private readonly IDeepSeekClient _deepSeek;
    private readonly CrawlAiOptions _options;

    public FieldFinderService(IDeepSeekClient deepSeek, IOptions<CrawlAiOptions> options)
    {
        _deepSeek = deepSeek;
        _options = options.Value;
    }

    public async Task<FieldFinderResponse> SuggestAsync(
        FieldFinderRequest request,
        CancellationToken cancellationToken = default)
    {
        var truncated = Truncate(request.HtmlOrText);
        var heuristic = HeuristicSuggestions(truncated, request.GoalHint);

        if (!_deepSeek.IsConfigured)
        {
            return new FieldFinderResponse(
                request.JobId,
                heuristic,
                UsedModel: false,
                DegradedReason: AiDegradedCopy.OfflineHeuristicSuggestions);
        }

        var system = """
            You help medical billing / RCM operators pick data fields from HTML snapshots.
            Return ONLY JSON: {"suggestions":[{"label":"...","cssSelector":"...","xpath":null,"reason":"...","confidence":0.0,"sampleValue":null}]}
            Suggest extraction targets the human must confirm. Never invent crawl link graphs or Domain Link Ratio.
            Prefer eligibility, claim, member, NPI, payer, and table cells when present.
            """;

        var user = $"""
            Goal hint: {request.GoalHint ?? "extract useful billing/eligibility fields"}
            Page URL: {request.PageUrl ?? "(unknown)"}
            HTML/text:
            {truncated}
            """;

        var result = await _deepSeek.CompleteAsync(system, user, cancellationToken).ConfigureAwait(false);
        if (!result.UsedModel || string.IsNullOrWhiteSpace(result.Content))
        {
            return new FieldFinderResponse(request.JobId, heuristic, false, result.DegradedReason);
        }

        var fromModel = TryParseSuggestions(result.Content);
        var merged = Merge(fromModel, heuristic);
        return new FieldFinderResponse(request.JobId, merged, true, null);
    }

    private string Truncate(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= _options.MaxContentChars)
        {
            return content ?? string.Empty;
        }

        return content[.._options.MaxContentChars];
    }

    internal static IReadOnlyList<FieldSuggestion> HeuristicSuggestions(string html, string? goalHint)
    {
        var list = new List<FieldSuggestion>();
        foreach (Match m in InputRegex.Matches(html))
        {
            var attrs = ParseAttrs(m.Value);
            var type = attrs.GetValueOrDefault("type", "text");
            if (string.Equals(type, "password", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = attrs.GetValueOrDefault("name") ?? attrs.GetValueOrDefault("id") ?? "field";
            var id = attrs.GetValueOrDefault("id");
            var placeholder = attrs.GetValueOrDefault("placeholder");
            var label = placeholder ?? name;
            var score = Score(label, goalHint);
            var selector = id is not null ? $"#{id}" : $"[name='{name}']";
            list.Add(new FieldSuggestion(
                label,
                selector,
                null,
                "Heuristic: input control detected in page snapshot.",
                score,
                null));
        }

        foreach (Match m in LabelishRegex.Matches(html))
        {
            var text = m.Groups[1].Value.Trim();
            if (text.Length < 3 || list.Count >= 12)
            {
                continue;
            }

            var score = Score(text, goalHint);
            if (score < 0.45)
            {
                continue;
            }

            list.Add(new FieldSuggestion(
                text,
                null,
                null,
                "Heuristic: labeled text near billing/eligibility vocabulary.",
                score,
                null));
        }

        return list
            .OrderByDescending(s => s.Confidence)
            .Take(10)
            .ToList();
    }

    private static double Score(string label, string? goalHint)
    {
        var hay = $"{label} {goalHint}".ToLowerInvariant();
        var hits = BillingHints.Count(h => hay.Contains(h, StringComparison.Ordinal));
        return Math.Clamp(0.35 + hits * 0.12, 0.35, 0.92);
    }

    private static Dictionary<string, string> ParseAttrs(string tag)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrRegex.Matches(tag))
        {
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        }

        return dict;
    }

    private static IReadOnlyList<FieldSuggestion> TryParseSuggestions(string content)
    {
        try
        {
            var json = ExtractJsonObject(content);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("suggestions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<FieldSuggestion>();
            }

            var list = new List<FieldSuggestion>();
            foreach (var el in arr.EnumerateArray())
            {
                var label = el.TryGetProperty("label", out var l) ? l.GetString() : null;
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                list.Add(new FieldSuggestion(
                    label!,
                    el.TryGetProperty("cssSelector", out var css) ? css.GetString() : null,
                    el.TryGetProperty("xpath", out var xp) ? xp.GetString() : null,
                    el.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "model" : "model",
                    el.TryGetProperty("confidence", out var conf) && conf.TryGetDouble(out var c) ? c : 0.5,
                    el.TryGetProperty("sampleValue", out var sample) ? sample.GetString() : null));
            }

            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<FieldSuggestion>();
        }
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return content[start..(end + 1)];
        }

        return content;
    }

    private static IReadOnlyList<FieldSuggestion> Merge(
        IReadOnlyList<FieldSuggestion> primary,
        IReadOnlyList<FieldSuggestion> fallback)
    {
        if (primary.Count == 0)
        {
            return fallback;
        }

        var seen = new HashSet<string>(primary.Select(p => p.Label), StringComparer.OrdinalIgnoreCase);
        var merged = primary.ToList();
        foreach (var f in fallback)
        {
            if (seen.Add(f.Label))
            {
                merged.Add(f);
            }
        }

        return merged.Take(12).ToList();
    }
}
