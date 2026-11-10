using System.Xml.Linq;
using Crawl.Domain.Links;
using Crawl.Infrastructure.Robots;
using Microsoft.Extensions.Logging;

namespace Crawl.Infrastructure.Html;

/// <summary>
/// When the seed returns few internal links, follow canonical / link rel hints and a bounded robots sitemap sample.
/// </summary>
public sealed class CrawlSeedLinkExpander(
    HtmlLinkExtractor linkExtractor,
    IRobotsTxtService robots,
    IHttpClientFactory httpClientFactory,
    ILogger<CrawlSeedLinkExpander> logger)
{
    private const int MaxSitemapLocs = 12;

    public async Task<IReadOnlyList<Uri>> ExpandAsync(
        string html,
        Uri pageUrl,
        string startHost,
        UrlNormalizationOptions normalizeOptions,
        CancellationToken cancellationToken)
    {
        var found = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(Uri? uri)
        {
            if (uri is null)
                return;
            if (!string.Equals(uri.IdnHost, startHost, StringComparison.OrdinalIgnoreCase))
                return;
            if (seen.Add(uri.AbsoluteUri))
                found.Add(uri);
        }

        foreach (var uri in linkExtractor.ExtractNavigationHints(html, pageUrl, normalizeOptions))
            Add(uri);

        foreach (var uri in await FetchSitemapSameHostAsync(pageUrl, startHost, cancellationToken))
            Add(uri);

        logger.LogDebug(
            "Seed expander added {Count} same-host URLs for {Host}",
            found.Count,
            startHost);

        return found;
    }

    private async Task<IReadOnlyList<Uri>> FetchSitemapSameHostAsync(
        Uri pageUrl,
        string startHost,
        CancellationToken cancellationToken)
    {
        var results = new List<Uri>();
        var sitemapUrls = await robots.GetSitemapUrlsAsync(pageUrl, cancellationToken);
        if (sitemapUrls.Count == 0)
            return results;

        var client = httpClientFactory.CreateClient("crawl-robots");
        foreach (var sitemapUrl in sitemapUrls.Take(2))
        {
            try
            {
                using var response = await client.GetAsync(sitemapUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue;

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                foreach (var loc in ParseSitemapLocs(body))
                {
                    if (!UrlNormalizer.TryNormalize(loc, out var normalized))
                        continue;
                    if (!string.Equals(normalized.IdnHost, startHost, StringComparison.OrdinalIgnoreCase))
                        continue;
                    results.Add(normalized);
                    if (results.Count >= MaxSitemapLocs)
                        return results;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                logger.LogDebug(ex, "Sitemap fetch failed for {Url}", sitemapUrl);
            }
        }

        return results;
    }

    private static IReadOnlyList<string> ParseSitemapLocs(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return [];

        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("loc", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value?.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Select(t => t!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
