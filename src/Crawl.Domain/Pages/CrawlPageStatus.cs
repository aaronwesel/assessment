namespace Crawl.Domain.Pages;

public enum CrawlPageStatus
{
    Queued = 0,
    Processing = 1,
    Done = 2,
    Failed = 3,
    Skipped = 4
}

public static class CrawlPageStatusExtensions
{
    public static bool IsTerminal(this CrawlPageStatus status) =>
        status is CrawlPageStatus.Done or CrawlPageStatus.Failed or CrawlPageStatus.Skipped;
}
