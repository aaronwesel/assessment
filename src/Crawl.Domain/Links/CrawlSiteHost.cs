namespace Crawl.Domain.Links;

/// <summary>
/// Same-site host matching for crawl graphs: apex host, www alias, and subdomains of the job seed host.
/// </summary>
public static class CrawlSiteHost
{
    public static bool IsSameSite(string linkHost, string crawlRootHost)
    {
        if (string.IsNullOrWhiteSpace(linkHost) || string.IsNullOrWhiteSpace(crawlRootHost))
            return false;

        var link = NormalizeHost(linkHost);
        var root = NormalizeHost(crawlRootHost);
        if (link.Length == 0 || root.Length == 0)
            return false;

        if (string.Equals(link, root, StringComparison.Ordinal))
            return true;

        var linkApex = StripLeadingWww(link);
        var rootApex = StripLeadingWww(root);
        if (string.Equals(linkApex, rootApex, StringComparison.Ordinal))
            return true;

        return link.EndsWith("." + rootApex, StringComparison.Ordinal);
    }

    public static bool IsSameSite(Uri link, string crawlRootHost) =>
        IsSameSite(link.IdnHost, crawlRootHost);

    private static string NormalizeHost(string host) =>
        host.TrimEnd('.').ToLowerInvariant();

    private static string StripLeadingWww(string host) =>
        host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
}
