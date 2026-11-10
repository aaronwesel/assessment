using Crawl.Domain.Pages;
using Crawl.Domain.Scheduling;

namespace Crawl.Domain.Tests.Scheduling;

/// <summary>
/// Multi-worker / at-least-once claim matrix (mirrors CrawlPageProcessor.EvaluateClaim).
/// </summary>
public class PageClaimResultTests
{
    // Mirror of worker EvaluateClaim kept here so Domain.Tests stay free of Worker refs.
    private static PageClaimResult Evaluate(CrawlPageStatus? status, bool redelivered, int queuedClaimRows)
    {
        if (status is null)
            return PageClaimResult.Missing;

        if (status.Value.IsTerminal())
            return PageClaimResult.AlreadyTerminal;

        if (queuedClaimRows > 0)
            return PageClaimResult.Claimed;

        if (status == CrawlPageStatus.Processing && redelivered)
            return PageClaimResult.Claimed;

        if (status == CrawlPageStatus.Processing)
            return PageClaimResult.OwnedByOther;

        if (status == CrawlPageStatus.Queued)
            return PageClaimResult.OwnedByOther;

        return PageClaimResult.AlreadyTerminal;
    }

    [Fact]
    public void Queued_claim_wins()
    {
        Assert.Equal(PageClaimResult.Claimed, Evaluate(CrawlPageStatus.Queued, redelivered: false, queuedClaimRows: 1));
    }

    [Fact]
    public void Duplicate_while_processing_skips_without_redelivery()
    {
        Assert.Equal(
            PageClaimResult.OwnedByOther,
            Evaluate(CrawlPageStatus.Processing, redelivered: false, queuedClaimRows: 0));
    }

    [Fact]
    public void Crash_redelivery_reclaims_processing()
    {
        Assert.Equal(
            PageClaimResult.Claimed,
            Evaluate(CrawlPageStatus.Processing, redelivered: true, queuedClaimRows: 0));
    }

    [Theory]
    [InlineData(CrawlPageStatus.Done)]
    [InlineData(CrawlPageStatus.Failed)]
    [InlineData(CrawlPageStatus.Skipped)]
    public void Terminal_is_idempotent_noop(CrawlPageStatus status)
    {
        Assert.Equal(PageClaimResult.AlreadyTerminal, Evaluate(status, redelivered: true, queuedClaimRows: 0));
        Assert.Equal(PageClaimResult.AlreadyTerminal, Evaluate(status, redelivered: false, queuedClaimRows: 1));
    }

    [Fact]
    public void Lost_queued_race_is_owned_by_other()
    {
        Assert.Equal(
            PageClaimResult.OwnedByOther,
            Evaluate(CrawlPageStatus.Queued, redelivered: false, queuedClaimRows: 0));
    }

    [Fact]
    public void Missing_page()
    {
        Assert.Equal(PageClaimResult.Missing, Evaluate(null, redelivered: false, queuedClaimRows: 0));
    }
}
