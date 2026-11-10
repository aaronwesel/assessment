namespace Crawl.Domain.Jobs;

public enum CrawlJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Canceled = 4
}

public static class CrawlJobStatusExtensions
{
    public static bool IsTerminal(this CrawlJobStatus status) =>
        status is CrawlJobStatus.Completed or CrawlJobStatus.Failed or CrawlJobStatus.Canceled;
}
