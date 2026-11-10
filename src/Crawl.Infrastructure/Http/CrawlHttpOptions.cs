namespace Crawl.Infrastructure.Http;

public sealed class CrawlHttpOptions
{
    public const string SectionName = "CrawlHttp";

    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;
}
