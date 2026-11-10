using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;

namespace Crawl.Api.Jobs;

public sealed record CreateJobRequest(string Url, int? MaxDepth = null, int? MaxPages = null);

public sealed record CreateJobResponse(Guid JobId);

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
    int PagesTotal,
    int PagesDone,
    int PagesFailed,
    int PagesQueued);

public sealed record JobHistoryItemDto(
    Guid JobId,
    string Url,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int PagesTotal);

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

public static class JobDtoMapper
{
    public static JobSummaryDto ToSummary(CrawlJob job, IReadOnlyList<CrawlPage> pages) =>
        new(
            job.Id,
            job.Url,
            job.Status.ToString(),
            job.MaxDepth,
            job.MaxPages,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.FailureReason,
            pages.Count,
            pages.Count(p => p.Status == CrawlPageStatus.Done),
            pages.Count(p => p.Status is CrawlPageStatus.Failed or CrawlPageStatus.Skipped),
            pages.Count(p => p.Status is CrawlPageStatus.Queued or CrawlPageStatus.Processing));

    public static PageTreeNodeDto? ToTree(IReadOnlyList<CrawlPage> pages)
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
