using Crawl.Ai.Alerts;
using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;
using Crawl.Domain.Links;

namespace Crawl.Ai.Tests;

public class JobSubscriberAlertTests
{
    [Fact]
    public void Status_template_stays_phi_free()
    {
        var job = CrawlJob.Create(new Uri("https://books.toscrape.com/"), 2, 50, DateTimeOffset.UtcNow);
        job.SetAlertEmail("operator@example.com");
        job.MarkFailed("Start page could not be crawled.", DateTimeOffset.UtcNow);

        var (subject, body) = JobSubscriberAlertTemplates.RenderStatus(job);
        Assert.Contains("job failed", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("books.toscrape.com", body, StringComparison.Ordinal);
        Assert.DoesNotContain("operator@", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Incident_template_stays_phi_free()
    {
        var jobId = Guid.NewGuid();
        UrlNormalizer.TryNormalize("https://example.com/", out var url);
        var job = CrawlJob.Create(url!, 1, 10, DateTimeOffset.UtcNow);
        job.SetAlertEmail("ops@example.com");

        var incident = CrawlIncident.Create(
            new CrawlFaultEvent(jobId, "corr", CrawlFaultCode.JobFailed, CrawlFaultSeverity.Error, "rate limited"),
            RemediationKind.AlertOnly);

        var (_, body) = JobSubscriberAlertTemplates.RenderIncident(job, incident, "job_failed");
        Assert.DoesNotContain("ops@", body, StringComparison.Ordinal);
        Assert.Contains("rate limited", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SetAlertEmail_rejects_invalid()
    {
        var job = CrawlJob.Create(new Uri("https://example.com/"), 1, 10, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => job.SetAlertEmail("not-an-email"));
        job.SetAlertEmail(null);
        Assert.False(job.HasAlertSubscription);
    }
}
