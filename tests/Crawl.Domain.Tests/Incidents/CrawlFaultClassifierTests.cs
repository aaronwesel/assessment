using Crawl.Domain.Incidents;

namespace Crawl.Domain.Tests.Incidents;

public class CrawlFaultClassifierTests
{
    [Fact]
    public void ClassifyHttp_429_is_rate_limited_warning()
    {
        var fault = CrawlFaultClassifier.ClassifyHttp(
            Guid.NewGuid(),
            "corr-1",
            429,
            host: "payer.example",
            retryAfterSeconds: 12);

        Assert.Equal(CrawlFaultCode.RateLimited, fault.Code);
        Assert.Equal(CrawlFaultSeverity.Warning, fault.Severity);
        Assert.Equal(12, fault.RetryAfterSeconds);
        Assert.Contains("RateLimited", fault.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("@", fault.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifyHttp_503_escalates_after_retries()
    {
        var fault = CrawlFaultClassifier.ClassifyHttp(
            Guid.NewGuid(),
            "corr-2",
            503,
            attempt: 3);

        Assert.Equal(CrawlFaultCode.ServiceUnavailable, fault.Code);
        Assert.Equal(CrawlFaultSeverity.Error, fault.Severity);
    }

    [Fact]
    public void ClassifyHttp_403_captcha_heuristic_is_critical()
    {
        var fault = CrawlFaultClassifier.ClassifyHttp(
            Guid.NewGuid(),
            "corr-3",
            403,
            responseSnippet: "<div id='cf-challenge'>captcha please verify you are human</div>");

        Assert.Equal(CrawlFaultCode.CaptchaOrBlock, fault.Code);
        Assert.Equal(CrawlFaultSeverity.Critical, fault.Severity);
    }

    [Fact]
    public void ClassifyException_timeout_and_dns()
    {
        var timeout = CrawlFaultClassifier.ClassifyException(
            Guid.NewGuid(),
            "c",
            new TaskCanceledException("timed out"));
        Assert.Equal(CrawlFaultCode.Timeout, timeout.Code);

        var dns = CrawlFaultClassifier.ClassifyException(
            Guid.NewGuid(),
            "c",
            new HttpRequestException("Name or service not known"));
        Assert.Equal(CrawlFaultCode.DnsFailure, dns.Code);
    }

    [Fact]
    public void SanitizeSummary_redacts_emails_and_passwords()
    {
        var cleaned = CrawlFaultClassifier.SanitizeSummary(
            "failure for ops@example.com password=super-secret");
        Assert.DoesNotContain("ops@example.com", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", cleaned, StringComparison.Ordinal);
        Assert.Contains("[redacted]", cleaned, StringComparison.Ordinal);
    }
}
