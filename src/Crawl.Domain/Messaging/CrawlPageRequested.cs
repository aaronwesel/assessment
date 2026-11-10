namespace Crawl.Domain.Messaging;

/// <summary>
/// One message per page crawl task. At-least-once delivery; consumers must be idempotent.
/// </summary>
public sealed record CrawlPageRequested(
    Guid JobId,
    long PageId,
    string Url,
    int Depth,
    DateTimeOffset EnqueuedAt);
