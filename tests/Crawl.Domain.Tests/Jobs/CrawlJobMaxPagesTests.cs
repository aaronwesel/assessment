using Crawl.Domain.Jobs;

namespace Crawl.Domain.Tests.Jobs;

public class CrawlJobMaxPagesTests
{
    [Fact]
    public void Default_and_absolute_max_pages_are_five_thousand()
    {
        Assert.Equal(5000, CrawlJob.DefaultMaxPages);
        Assert.Equal(5000, CrawlJob.AbsoluteMaxPages);
    }

    [Fact]
    public void Create_accepts_at_hard_cap()
    {
        var job = CrawlJob.Create(
            new Uri("https://example.com/"),
            maxDepth: 2,
            maxPages: CrawlJob.AbsoluteMaxPages,
            DateTimeOffset.UtcNow);

        Assert.Equal(CrawlJob.AbsoluteMaxPages, job.MaxPages);
    }

    [Fact]
    public void Create_rejects_above_hard_cap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrawlJob.Create(
                new Uri("https://example.com/"),
                maxDepth: 2,
                maxPages: CrawlJob.AbsoluteMaxPages + 1,
                DateTimeOffset.UtcNow));
    }
}
