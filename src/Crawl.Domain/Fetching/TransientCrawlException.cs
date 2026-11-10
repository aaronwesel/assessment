using Crawl.Domain.Incidents;

namespace Crawl.Domain.Fetching;

/// <summary>Signals the consumer to retry (or DLQ after MaxRetryAttempts).</summary>
public sealed class TransientCrawlException : Exception
{
    public TransientCrawlException(string message) : base(message) { }

    public TransientCrawlException(string message, Exception inner) : base(message, inner) { }

    public TransientCrawlException(
        string message,
        CrawlFaultCode faultCode,
        int? httpStatus = null,
        int? retryAfterSeconds = null,
        string? host = null,
        Exception? inner = null)
        : base(message, inner)
    {
        FaultCode = faultCode;
        HttpStatus = httpStatus;
        RetryAfterSeconds = retryAfterSeconds;
        Host = host;
    }

    public CrawlFaultCode? FaultCode { get; }
    public int? HttpStatus { get; }
    public int? RetryAfterSeconds { get; }
    public string? Host { get; }
}
