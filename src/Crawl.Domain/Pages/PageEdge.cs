namespace Crawl.Domain.Pages;

/// <summary>Outgoing link edge from a crawled page. Targets may be external and never crawled.</summary>
public class PageEdge
{
    private PageEdge() { }

    public long Id { get; private set; }
    public long SourcePageId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }

    public static PageEdge Create(long sourcePageId, Uri normalizedTargetUrl, bool isInternal) =>
        new()
        {
            SourcePageId = sourcePageId,
            TargetUrl = normalizedTargetUrl.AbsoluteUri,
            IsInternal = isInternal
        };
}
