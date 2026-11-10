namespace Crawl.Domain.Incidents;

/// <summary>
/// Structured crawl fault taxonomy emitted by worker/API.
/// Deterministic classification — AI may advise remediations but must not invent fault codes.
/// </summary>
public enum CrawlFaultCode
{
    Unknown = 0,
    RateLimited = 1,
    ServiceUnavailable = 2,
    Timeout = 3,
    DnsFailure = 4,
    ConnectionReset = 5,
    CaptchaOrBlock = 6,
    HardBlock = 7,
    OtherTransient = 8,
    JobFailed = 9,
    RepeatedDlq = 10
}
