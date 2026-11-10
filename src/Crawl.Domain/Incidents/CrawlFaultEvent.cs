namespace Crawl.Domain.Incidents;

/// <summary>
/// PHI-safe structured fault for monitoring. Never put secrets, passwords, or page body content here.
/// </summary>
public sealed record CrawlFaultEvent(
    Guid JobId,
    string CorrelationId,
    CrawlFaultCode Code,
    CrawlFaultSeverity Severity,
    string Summary,
    string? Host = null,
    long? PageId = null,
    int? HttpStatus = null,
    int? RetryAfterSeconds = null,
    int Attempt = 1,
    DateTimeOffset? OccurredAt = null)
{
    public DateTimeOffset At { get; init; } = OccurredAt ?? DateTimeOffset.UtcNow;
}
