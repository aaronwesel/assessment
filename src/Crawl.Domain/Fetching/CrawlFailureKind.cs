namespace Crawl.Domain.Fetching;

/// <summary>
/// Retry taxonomy for the worker consumer.
/// Transient → republish with x-retry-count; Poison → DLQ; Terminal page outcomes are not exceptions.
/// </summary>
public enum CrawlFailureKind
{
    /// <summary>Network blip, timeout, 408/429/5xx — safe to retry.</summary>
    Transient = 0,

    /// <summary>Malformed payload / unrecoverable processor bug — send to DLQ, do not retry.</summary>
    Poison = 1
}
