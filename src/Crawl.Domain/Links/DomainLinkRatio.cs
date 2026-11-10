namespace Crawl.Domain.Links;

/// <summary>
/// Domain Link Ratio = (# outgoing links within the starting domain) / (total outgoing links).
/// Zero when the page has no outgoing links. Deterministic; not AI-assisted.
/// </summary>
public static class DomainLinkRatio
{
    public static decimal Calculate(int internalLinks, int totalLinks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(internalLinks);
        ArgumentOutOfRangeException.ThrowIfNegative(totalLinks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(internalLinks, totalLinks);

        return totalLinks == 0 ? 0m : (decimal)internalLinks / totalLinks;
    }
}
