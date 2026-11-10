using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;
using Microsoft.Extensions.Logging;

namespace Crawl.Ai.Alerts;

/// <summary>
/// Emails the per-job subscriber address saved on guided step 5. No-op when SES/SMTP is not configured.
/// </summary>
public sealed class JobSubscriberAlertService(
    ICrawlSesEmailTransport transport,
    ILogger<JobSubscriberAlertService> logger) : IJobSubscriberAlertService
{
    public async Task TrySendTerminalStatusAlertAsync(CrawlJob job, CancellationToken cancellationToken = default)
    {
        if (!job.HasAlertSubscription || !job.Status.IsTerminal())
            return;

        if (!job.TryMarkStatusAlertSent())
            return;

        if (!transport.IsReady)
        {
            logger.LogInformation(
                "Job status alert skipped (transport not ready) job={JobId} status={Status}",
                job.Id,
                job.Status);
            return;
        }

        var (subject, body) = JobSubscriberAlertTemplates.RenderStatus(job);
        var sent = await transport.TrySendAsync(job.AlertEmail!, subject, body, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Job status subscriber alert attempted job={JobId} status={Status} sent={Sent}",
            job.Id,
            job.Status,
            sent);
    }

    public async Task TrySendIncidentAlertAsync(
        CrawlJob job,
        CrawlIncident incident,
        string alertCode,
        CancellationToken cancellationToken = default)
    {
        if (!job.HasAlertSubscription)
            return;

        if (!transport.IsReady)
        {
            logger.LogDebug(
                "Job incident subscriber alert skipped (transport not ready) job={JobId}",
                job.Id);
            return;
        }

        var (subject, body) = JobSubscriberAlertTemplates.RenderIncident(job, incident, alertCode);
        var sent = await transport.TrySendAsync(job.AlertEmail!, subject, body, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Job incident subscriber alert attempted job={JobId} fault={Fault} sent={Sent}",
            job.Id,
            incident.FaultCode,
            sent);
    }
}
