using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;

namespace Crawl.Domain.Messaging;

/// <summary>
/// Live job progress snapshot for SignalR fan-out. PHI-safe: job metadata + counts only (no HTML/passwords).
/// </summary>
public sealed record CrawlJobProgressEvent(
    Guid JobId,
    string Status,
    int ProgressPercent,
    int Total,
    int Queued,
    int Processing,
    int Done,
    int Skipped,
    int Failed,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    string EventKind,
    DateTimeOffset OccurredAt)
{
    public const string KindCreated = "created";
    public const string KindPageProgress = "pageProgress";
    public const string KindJobStatus = "jobStatus";
    public const string KindCanceled = "canceled";

    public static CrawlJobProgressEvent FromJobAndPages(
        CrawlJob job,
        IReadOnlyList<CrawlPage> pages,
        string eventKind,
        DateTimeOffset? occurredAt = null)
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
            job.Status.ToString(),
            progress,
            pages.Count,
            queued,
            processing,
            done,
            skipped,
            failed,
            job.StartedAt,
            job.CompletedAt,
            job.FailureReason,
            eventKind,
            occurredAt ?? DateTimeOffset.UtcNow);
    }
}
