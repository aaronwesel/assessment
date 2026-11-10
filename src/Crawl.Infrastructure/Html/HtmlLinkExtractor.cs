using AngleSharp.Html.Parser;
using Crawl.Domain.Links;

namespace Crawl.Infrastructure.Html;

/// <summary>
/// Deterministic HTML &lt;a href&gt; extraction (AngleSharp). Honors &lt;base href&gt;; drops non-http(s) and
/// duplicate normalized targets. Domain Link Ratio uses only these links — AI must never replace this path.
/// </summary>
public sealed class HtmlLinkExtractor
{
    private readonly HtmlParser _parser = new();

    public IReadOnlyList<Uri> Extract(string html, Uri pageUrl) =>
        Extract(html, pageUrl, UrlNormalizationOptions.Default);

    public IReadOnlyList<Uri> Extract(string html, Uri pageUrl, UrlNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var document = _parser.ParseDocument(html);
        var baseUri = ResolveBase(document, pageUrl, options);
        return CollectHrefs(document, baseUri, options, includeStructuralHints: true);
    }

    /// <summary>
    /// Canonical / link-relnext hints used only for seed expansion (not DLR denominator inflation).
    /// </summary>
    public IReadOnlyList<Uri> ExtractNavigationHints(string html, Uri pageUrl, UrlNormalizationOptions options)
    {
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var document = _parser.ParseDocument(html);
        var baseUri = ResolveBase(document, pageUrl, options);
        var hints = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in document.QuerySelectorAll("link[href]"))
        {
            var rel = link.GetAttribute("rel") ?? string.Empty;
            if (!RelIsNavigational(rel))
                continue;

            TryAdd(link.GetAttribute("href"), baseUri, options, seen, hints);
        }

        TryAddMetaRefresh(document, baseUri, options, seen, hints);

        foreach (var node in document.QuerySelectorAll("[data-href], [data-url]"))
        {
            TryAdd(node.GetAttribute("data-href") ?? node.GetAttribute("data-url"), baseUri, options, seen, hints);
        }

        return hints;
    }

    private IReadOnlyList<Uri> CollectHrefs(
        AngleSharp.Dom.IDocument document,
        Uri baseUri,
        UrlNormalizationOptions options,
        bool includeStructuralHints)
    {
        var results = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
            TryAdd(anchor.GetAttribute("href"), baseUri, options, seen, results);

        foreach (var area in document.QuerySelectorAll("area[href]"))
            TryAdd(area.GetAttribute("href"), baseUri, options, seen, results);

        if (includeStructuralHints)
        {
            foreach (var link in document.QuerySelectorAll("link[href]"))
            {
                var rel = link.GetAttribute("rel") ?? string.Empty;
                if (!RelIsNavigational(rel))
                    continue;
                TryAdd(link.GetAttribute("href"), baseUri, options, seen, results);
            }

            TryAddMetaRefresh(document, baseUri, options, seen, results);
        }

        return results;
    }

    private static Uri ResolveBase(AngleSharp.Dom.IDocument document, Uri pageUrl, UrlNormalizationOptions options)
    {
        var baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(baseHref)
            && UrlNormalizer.TryNormalize(baseHref, pageUrl, options, out var parsedBase))
            return parsedBase;
        return pageUrl;
    }

    private static bool RelIsNavigational(string rel)
    {
        var tokens = rel.Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (token.Equals("canonical", StringComparison.OrdinalIgnoreCase)
                || token.Equals("alternate", StringComparison.OrdinalIgnoreCase)
                || token.Equals("next", StringComparison.OrdinalIgnoreCase)
                || token.Equals("prev", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryAddMetaRefresh(
        AngleSharp.Dom.IDocument document,
        Uri baseUri,
        UrlNormalizationOptions options,
        HashSet<string> seen,
        List<Uri> results)
    {
        var refresh = document.QuerySelector("meta[http-equiv]");
        if (refresh?.GetAttribute("http-equiv") is not { } httpEquiv
            || !httpEquiv.Equals("refresh", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var content = refresh?.GetAttribute("content");
        if (string.IsNullOrWhiteSpace(content))
            return;

        var urlPart = content.Split(';', StringSplitOptions.TrimEntries).LastOrDefault();
        if (urlPart is null)
            return;

        const string prefix = "url=";
        if (urlPart.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            urlPart = urlPart[prefix.Length..];

        TryAdd(urlPart.Trim('\'', '"', ' '), baseUri, options, seen, results);
    }

    private static void TryAdd(
        string? href,
        Uri baseUri,
        UrlNormalizationOptions options,
        HashSet<string> seen,
        List<Uri> results)
    {
        if (!UrlNormalizer.TryNormalize(href, baseUri, options, out var normalized))
            return;

        if (seen.Add(normalized.AbsoluteUri))
            results.Add(normalized);
    }
}
