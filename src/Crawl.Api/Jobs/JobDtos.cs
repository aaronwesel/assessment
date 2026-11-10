using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;

namespace Crawl.Api.Jobs;

public sealed record CreateJobRequest(string Url, int? MaxDepth = null, int? MaxPages = null);

public sealed record CreateJobResponse(Guid JobId);

public sealed record PageCountsDto(
    int Total,
    int Queued,
    int Processing,
    int Done,
    int Skipped,
    int Failed);

public sealed record JobSummaryDto(
    Guid JobId,
    string Url,
    string Status,
    int MaxDepth,
    int MaxPages,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    PageCountsDto PageCounts,
    /// <summary>0–100 progress from terminal pages / max(total, maxPages budget signal).</summary>
    int ProgressPercent);

public sealed record JobHistoryItemDto(
    Guid JobId,
    string Url,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record PagedHistoryDto(IReadOnlyList<JobHistoryItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record PageTreeNodeDto(
    long PageId,
    string Url,
    int Depth,
    string Status,
    decimal? DomainLinkRatio,
    int? LinkCount,
    int? InternalLinkCount,
    string? Error,
    IReadOnlyList<PageTreeNodeDto> Children);

public sealed record JobTreeDto(Guid JobId, string Status, PageTreeNodeDto? Root);

public sealed record PageLinkDto(string Url, bool IsInternal);

/// <summary>Flat page list for exports and live job details (tree may still be partial).</summary>
public sealed record PageRowDto(
    long PageId,
    string Url,
    int Depth,
    string Status,
    decimal? DomainLinkRatio,
    int? LinkCount,
    int? InternalLinkCount,
    string? Error,
    long? ParentPageId);

public static class JobDtoMapper
{
    public static JobSummaryDto ToSummary(CrawlJob job, IReadOnlyList<CrawlPage> pages)
    {
        var queued = pages.Count(p => p.Status == CrawlPageStatus.Queued);
        var processing = pages.Count(p => p.Status == CrawlPageStatus.Processing);
        var done = pages.Count(p => p.Status == CrawlPageStatus.Done);
        var skipped = pages.Count(p => p.Status == CrawlPageStatus.Skipped);
        var failed = pages.Count(p => p.Status == CrawlPageStatus.Failed);
        var terminal = done + skipped + failed;
        var denominator = Math.Max(pages.Count, 1);
        var progress = job.Status.IsTerminal()
            ? 100
            : (int)Math.Clamp(Math.Round(100.0 * terminal / denominator), 0, 99);

        return new(
            job.Id,
            job.Url,
            job.Status.ToString(),
            job.MaxDepth,
            job.MaxPages,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.FailureReason,
            new PageCountsDto(pages.Count, queued, processing, done, skipped, failed),
            progress);
    }

    public static JobTreeDto ToTree(CrawlJob job, IReadOnlyList<CrawlPage> pages) =>
        new(job.Id, job.Status.ToString(), BuildRoot(pages));

    public static IReadOnlyList<PageRowDto> ToFlatPages(IReadOnlyList<CrawlPage> pages) =>
        pages
            .OrderBy(p => p.Depth)
            .ThenBy(p => p.Url)
            .Select(p => new PageRowDto(
                p.Id,
                p.Url,
                p.Depth,
                p.Status.ToString(),
                p.DomainLinkRatioValue,
                p.LinkCount,
                p.InternalLinkCount,
                p.Error,
                p.ParentPageId))
            .ToList();

    private static PageTreeNodeDto? BuildRoot(IReadOnlyList<CrawlPage> pages)
    {
        var byParent = pages.ToLookup(p => p.ParentPageId);
        var root = pages.FirstOrDefault(p => p.ParentPageId is null && p.Depth == 0);
        if (root is null)
            return null;

        return Build(root);

        PageTreeNodeDto Build(CrawlPage page) =>
            new(
                page.Id,
                page.Url,
                page.Depth,
                page.Status.ToString(),
                page.DomainLinkRatioValue,
                page.LinkCount,
                page.InternalLinkCount,
                page.Error,
                byParent[page.Id].OrderBy(c => c.Url).Select(Build).ToList());
    }
}
