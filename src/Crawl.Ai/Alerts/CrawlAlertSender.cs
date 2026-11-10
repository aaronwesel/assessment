using Crawl.Ai.Options;
using Crawl.Domain.Incidents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.Alerts;

/// <summary>
/// Optional ops email alerts (SMTP or SES). Feature-flagged via Crawl:Alerts:EmailEnabled.
/// Never emails passwords. Bodies are PHI-guarded.
/// </summary>
public sealed class CrawlAlertSender(
    IOptions<CrawlAlertOptions> options,
    ICrawlSesEmailTransport transport,
    ILogger<CrawlAlertSender> logger) : ICrawlAlertSender
{
    public async Task<bool> TrySendIncidentAlertAsync(
        CrawlIncident incident,
        string alertCode,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.EmailEnabled)
            return false;

        if (string.IsNullOrWhiteSpace(opts.ToAddress) || string.IsNullOrWhiteSpace(opts.FromAddress))
        {
            logger.LogDebug("Crawl email alert skipped: From/To not configured");
            return false;
        }

        var (subject, body) = CrawlAlertTemplates.RenderIncident(incident, alertCode);

        if (!transport.IsReady)
        {
            logger.LogInformation(
                "Crawl ops alert skipped (transport not ready) alert_code={AlertCode} job={JobId}",
                alertCode,
                incident.JobId);
            return false;
        }

        var sent = await transport.TrySendAsync(opts.ToAddress, subject, body, cancellationToken)
            .ConfigureAwait(false);
        if (sent)
        {
            logger.LogInformation("Crawl ops alert sent alert_code={AlertCode} job={JobId}", alertCode, incident.JobId);
        }

        return sent;
    }
}
