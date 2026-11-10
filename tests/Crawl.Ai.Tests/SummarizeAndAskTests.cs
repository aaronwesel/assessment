using Crawl.Ai.AskTheCrawl;
using Crawl.Ai.Models;
using Crawl.Ai.Summarization;

namespace Crawl.Ai.Tests;

public class SummarizeAndAskTests
{
    [Fact]
    public void LocalSummary_cites_provided_low_ratio_without_computing()
    {
        var jobId = Guid.NewGuid();
        var pages = new[]
        {
            new CrawlPageSnapshot(Guid.NewGuid(), "https://a.example/", 0, 0.9, "ok", null),
            new CrawlPageSnapshot(Guid.NewGuid(), "https://a.example/low", 1, 0.1, "ok", null),
            new CrawlPageSnapshot(Guid.NewGuid(), "https://a.example/err", 1, 0.5, "error", "timeout")
        };

        var resp = ResultSummarizer.LocalSummary(new SummarizeRequest(
            jobId, "https://a.example/", "Completed", 3, 2, pages, null));

        Assert.Contains(resp.Highlights, h => h.Contains("Domain Link Ratio < 0.2"));
        Assert.Contains(resp.CitedUrls, u => u.Contains("/low"));
    }

    [Fact]
    public async Task Summarize_degrades_without_key()
    {
        var svc = new ResultSummarizer(new FakeDeepSeek(false));
        var resp = await svc.SummarizeAsync(new SummarizeRequest(
            Guid.NewGuid(), "https://x", "Completed", 1, 0,
            new[] { new CrawlPageSnapshot(Guid.NewGuid(), "https://x", 0, 1.0, "ok", null) },
            null));
        Assert.False(resp.UsedModel);
        Assert.NotNull(resp.DegradedReason);
    }

    [Fact]
    public void Ask_filters_on_provided_ratio_only()
    {
        var pages = new[]
        {
            new CrawlPageSnapshot(Guid.NewGuid(), "https://a/high", 0, 0.8, "ok", null),
            new CrawlPageSnapshot(Guid.NewGuid(), "https://a/low", 1, 0.15, "ok", null)
        };

        var resp = AskTheCrawlService.LocalAnswer(new AskTheCrawlRequest(
            Guid.NewGuid(),
            "which pages have ratio < 0.2?",
            pages));

        Assert.Single(resp.Citations);
        Assert.Equal("https://a/low", resp.Citations[0].Url);
        Assert.Contains("0.2", resp.Answer);
    }

    [Fact]
    public async Task Ask_degrades_gracefully()
    {
        var svc = new AskTheCrawlService(new FakeDeepSeek(false));
        var resp = await svc.AskAsync(new AskTheCrawlRequest(
            Guid.NewGuid(), "any failures?", Array.Empty<CrawlPageSnapshot>()));
        Assert.False(resp.UsedModel);
    }
}
