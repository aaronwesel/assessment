using Crawl.Domain.Jobs;
using Crawl.Domain.Messaging;
using Crawl.Domain.Pages;

namespace Crawl.Domain.Tests;

public class CrawlJobProgressEventTests
{
    [Fact]
    public void FromJobAndPages_computes_counts_and_progress()
    {
        var now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
        var job = CrawlJob.Create(new Uri("https://example.com/"), 2, 50, now);
        job.MarkRunning(now);

        var root = CrawlPage.CreateRoot(job.Id, new Uri("https://example.com/"), now);
        root.MarkDone(3, 2, now);
        var child = CrawlPage.CreateChild(job.Id, root.Id, 1, new Uri("https://example.com/a"), now);

        var evt = CrawlJobProgressEvent.FromJobAndPages(
            job,
            [root, child],
            CrawlJobProgressEvent.KindPageProgress,
            now);

        Assert.Equal(job.Id, evt.JobId);
        Assert.Equal("Running", evt.Status);
        Assert.Equal(2, evt.Total);
        Assert.Equal(1, evt.Done);
        Assert.Equal(1, evt.Queued);
        Assert.Equal(50, evt.ProgressPercent);
        Assert.Equal(CrawlJobProgressEvent.KindPageProgress, evt.EventKind);
        Assert.Null(evt.FailureReason);
    }

    [Fact]
    public void FromJobAndPages_terminal_job_is_100_percent()
    {
        var now = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
        var job = CrawlJob.Create(new Uri("https://example.com/"), 1, 10, now);
        job.MarkCanceled(now);
        var root = CrawlPage.CreateRoot(job.Id, new Uri("https://example.com/"), now);

        var evt = CrawlJobProgressEvent.FromJobAndPages(job, [root], CrawlJobProgressEvent.KindCanceled, now);

        Assert.Equal(100, evt.ProgressPercent);
        Assert.Equal("Canceled", evt.Status);
    }
}
