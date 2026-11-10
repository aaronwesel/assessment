namespace Crawl.Domain.Links;

/// <summary>
/// Crawl-oriented URL canonicalization knobs (Scrapy-inspired, not a full Scrapy port).
/// Defaults favor aggressive de-duplication for job crawlers.
/// </summary>
public sealed class UrlNormalizationOptions
{
    public static UrlNormalizationOptions Default { get; } = new();

    /// <summary>
    /// When true, query parameters are sorted by name then value so
    /// <c>?b=1&amp;a=2</c> and <c>?a=2&amp;b=1</c> collapse to one URL.
    /// Empty query is removed. Parameter order is not significant for our DLR / graph.
    /// </summary>
    public bool SortQueryParameters { get; init; } = true;

    /// <summary>
    /// Maximum absolute URI length after normalization (matches job/page column limit).
    /// </summary>
    public int MaxUrlLength { get; init; } = 2048;
}
