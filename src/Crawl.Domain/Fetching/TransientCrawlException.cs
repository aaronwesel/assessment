namespace Crawl.Domain.Fetching;

/// <summary>Signals the consumer to retry (or DLQ after MaxRetryAttempts).</summary>
public sealed class TransientCrawlException : Exception
{
    public TransientCrawlException(string message) : base(message) { }

    public TransientCrawlException(string message, Exception inner) : base(message, inner) { }
}
