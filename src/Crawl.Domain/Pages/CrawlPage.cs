using Crawl.Domain.Links;

namespace Crawl.Domain.Pages;

public class CrawlPage
{
    public const int ErrorMaxLength = 500;

    private CrawlPage() { }

    public long Id { get; private set; }
    public Guid JobId { get; private set; }
    public long? ParentPageId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public int Depth { get; private set; }
    public CrawlPageStatus Status { get; private set; }
    public int? LinkCount { get; private set; }
    public int? InternalLinkCount { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public ICollection<PageEdge> Edges { get; private set; } = new List<PageEdge>();

    public decimal? DomainLinkRatioValue =>
        Status == CrawlPageStatus.Done && LinkCount is { } total && InternalLinkCount is { } internalCount
            ? DomainLinkRatio.Calculate(internalCount, total)
            : null;

    public static CrawlPage CreateRoot(Guid jobId, Uri normalizedUrl, DateTimeOffset createdAtUtc) =>
        new()
        {
            JobId = jobId,
            Url = normalizedUrl.AbsoluteUri,
            Depth = 0,
            Status = CrawlPageStatus.Queued,
            CreatedAt = createdAtUtc
        };

    public static CrawlPage CreateChild(
        Guid jobId,
        long parentPageId,
        int depth,
        Uri normalizedUrl,
        DateTimeOffset createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);

        return new CrawlPage
        {
            JobId = jobId,
            ParentPageId = parentPageId,
            Url = normalizedUrl.AbsoluteUri,
            Depth = depth,
            Status = CrawlPageStatus.Queued,
            CreatedAt = createdAtUtc
        };
    }

    public void MarkProcessing()
    {
        if (Status is CrawlPageStatus.Queued or CrawlPageStatus.Processing)
            Status = CrawlPageStatus.Processing;
    }

    public void MarkDone(int linkCount, int internalLinkCount, DateTimeOffset finishedAtUtc)
    {
        Status = CrawlPageStatus.Done;
        LinkCount = linkCount;
        InternalLinkCount = internalLinkCount;
        Error = null;
        FinishedAt = finishedAtUtc;
    }

    public void MarkFailed(string error, DateTimeOffset finishedAtUtc)
    {
        Status = CrawlPageStatus.Failed;
        Error = TruncateError(error);
        FinishedAt = finishedAtUtc;
    }

    public void MarkSkipped(string reason, DateTimeOffset finishedAtUtc)
    {
        Status = CrawlPageStatus.Skipped;
        Error = TruncateError(reason);
        FinishedAt = finishedAtUtc;
    }

    public static string? TruncateError(string? error) =>
        error is null || error.Length <= ErrorMaxLength ? error : error[..ErrorMaxLength];
}
