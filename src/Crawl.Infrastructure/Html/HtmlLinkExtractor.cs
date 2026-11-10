using AngleSharp.Html.Parser;
using Crawl.Domain.Links;

namespace Crawl.Infrastructure.Html;

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
        var baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        var baseUri = pageUrl;
        if (!string.IsNullOrWhiteSpace(baseHref)
            && UrlNormalizer.TryNormalize(baseHref, pageUrl, options, out var parsedBase))
            baseUri = parsedBase;

        var results = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");
            if (!UrlNormalizer.TryNormalize(href, baseUri, options, out var normalized))
                continue;

            if (seen.Add(normalized.AbsoluteUri))
                results.Add(normalized);
        }

        return results;
    }
}
