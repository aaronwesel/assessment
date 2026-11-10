namespace Crawl.Domain.Messaging;

/// <summary>
/// Lightweight job progress snapshot for SignalR relay (no HTML, no secrets).
/// Published by workers after page reconcile; API fans out to Job Details clients.
/// </summary>
public sealed record CrawlJobProgressEvent(
    Guid JobId,
    string Status,
    int ProgressPercent,
    int TotalPages,
    int Queued,
    int Processing,
    int Done,
    int Skipped,
    int Failed,
    DateTimeOffset AtUtc);
