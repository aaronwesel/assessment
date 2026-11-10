using AngleSharp.Html.Parser;
using Crawl.Domain.Links;

namespace Crawl.Infrastructure.Html;

public sealed class HtmlLinkExtractor
{
    private readonly HtmlParser _parser = new();

    public IReadOnlyList<Uri> Extract(string html, Uri pageUrl)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        if (string.IsNullOrWhiteSpace(html))
            return [];

        var document = _parser.ParseDocument(html);
        var baseHref = document.QuerySelector("base[href]")?.GetAttribute("href");
        var baseUri = pageUrl;
        if (!string.IsNullOrWhiteSpace(baseHref) && UrlNormalizer.TryNormalize(baseHref, pageUrl, out var parsedBase))
            baseUri = parsedBase;

        var results = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = anchor.GetAttribute("href");
            if (!UrlNormalizer.TryNormalize(href, baseUri, out var normalized))
                continue;

            if (seen.Add(normalized.AbsoluteUri))
                results.Add(normalized);
        }

        return results;
    }
}
