using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;

namespace Crawl.Ai.Alerts;

/// <summary>PHI-free copy for per-job subscriber alerts (guided step 5).</summary>
public static class JobSubscriberAlertTemplates
{
    public static (string Subject, string Body) RenderStatus(CrawlJob job)
    {
        var code = job.Status switch
        {
            CrawlJobStatus.Completed => "job_completed",
            CrawlJobStatus.Failed => "job_failed",
            CrawlJobStatus.Canceled => "job_canceled",
            _ => "job_status"
        };

        var subject = $"[Trailmap crawl] Your job {code.Replace('_', ' ')}";
        var body =
            $"event={code}\n" +
            $"job_id={job.Id:N}\n" +
            $"status={job.Status}\n" +
            $"seed_host={job.StartHost}\n" +
            $"completed_at={job.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            $"failure_reason={CrawlFaultClassifier.SanitizeSummary(job.FailureReason ?? "(none)")}\n" +
            "note=Open your guided crawl job details page to export CSV/JSON and review Domain Link Ratio.\n" +
            "note=This message intentionally omits URLs, credentials, and page content.\n";

        CrawlAlertTemplates.AssertNoPhi(body);
        return (subject, body);
    }

    public static (string Subject, string Body) RenderIncident(CrawlJob job, CrawlIncident incident, string alertCode)
    {
        if (string.IsNullOrWhiteSpace(alertCode))
            alertCode = "crawl_incident";

        var subject = $"[Trailmap crawl] Issue on your job ({alertCode.Replace('_', ' ')})";
        var body =
            $"event={alertCode}\n" +
            $"job_id={job.Id:N}\n" +
            $"job_status={job.Status}\n" +
            $"seed_host={job.StartHost}\n" +
            $"occurred_at={incident.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            $"correlation_id={incident.CorrelationId}\n" +
            $"fault_code={incident.FaultCode}\n" +
            $"severity={incident.Severity}\n" +
            $"remediation={incident.RemediationKind}/{incident.RemediationStatus}\n" +
            $"summary={CrawlFaultClassifier.SanitizeSummary(incident.Summary)}\n" +
            "note=Credential and PHI fields are omitted from crawl alerts.\n";

        CrawlAlertTemplates.AssertNoPhi(body);
        return (subject, body);
    }
}
