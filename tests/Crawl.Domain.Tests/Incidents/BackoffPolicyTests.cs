using Crawl.Domain.Incidents;

namespace Crawl.Domain.Tests.Incidents;

public class BackoffPolicyTests
{
    [Fact]
    public void ComputeDelay_prefers_retry_after()
    {
        var delay = BackoffPolicy.ComputeDelay(
            attempt: 5,
            retryAfterSeconds: 30,
            jitterFactor: 0);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void ComputeDelay_exponential_without_jitter()
    {
        var d1 = BackoffPolicy.ComputeDelay(1, baseDelay: TimeSpan.FromSeconds(1), jitterFactor: 0);
        var d2 = BackoffPolicy.ComputeDelay(2, baseDelay: TimeSpan.FromSeconds(1), jitterFactor: 0);
        var d3 = BackoffPolicy.ComputeDelay(3, baseDelay: TimeSpan.FromSeconds(1), jitterFactor: 0);

        Assert.Equal(TimeSpan.FromSeconds(1), d1);
        Assert.Equal(TimeSpan.FromSeconds(2), d2);
        Assert.Equal(TimeSpan.FromSeconds(4), d3);
    }

    [Fact]
    public void ComputeDelay_caps_at_max()
    {
        var delay = BackoffPolicy.ComputeDelay(
            attempt: 20,
            baseDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(15),
            jitterFactor: 0);

        Assert.Equal(TimeSpan.FromSeconds(15), delay);
    }

    [Fact]
    public void NextPolitenessMultiplier_bumps_and_caps()
    {
        var next = BackoffPolicy.NextPolitenessMultiplier(1.0, bump: 2.0, max: 8.0);
        Assert.Equal(2.0, next);
        var capped = BackoffPolicy.NextPolitenessMultiplier(8.0, bump: 2.0, max: 8.0);
        Assert.Equal(8.0, capped);
    }

    [Theory]
    [InlineData(CrawlFaultCode.RateLimited, RemediationKind.BackoffAndPolitenessBump)]
    [InlineData(CrawlFaultCode.HardBlock, RemediationKind.SwitchEgressProxy)]
    [InlineData(CrawlFaultCode.CaptchaOrBlock, RemediationKind.SwitchEgressProxy)]
    [InlineData(CrawlFaultCode.Timeout, RemediationKind.BackoffOnly)]
    [InlineData(CrawlFaultCode.JobFailed, RemediationKind.AlertOnly)]
    [InlineData(CrawlFaultCode.RepeatedDlq, RemediationKind.AlertOnly)]
    public void SuggestRemediation_maps_fault_codes(CrawlFaultCode code, RemediationKind expected)
    {
        Assert.Equal(expected, BackoffPolicy.SuggestRemediation(code));
    }
}
