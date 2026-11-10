using System.Net;
using System.Net.Mail;
using Crawl.Ai.Options;
using Crawl.Domain.Incidents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.Alerts;

/// <summary>
/// Optional email alerts (SMTP or SES-shaped stub). Feature-flagged via Crawl:Alerts:EmailEnabled.
/// Never emails passwords. Bodies are PHI-guarded.
/// </summary>
public sealed class CrawlAlertSender(
    IOptions<CrawlAlertOptions> options,
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

        // Recipients are ops mailboxes from config — never put them in the PHI-guarded body.
        var (subject, body) = CrawlAlertTemplates.RenderIncident(incident, alertCode);
        var provider = (opts.Provider ?? "smtp").Trim().ToLowerInvariant();

        try
        {
            if (provider is "stub" or "ses")
            {
                // SES: reuse platform prep pattern (identity + task role in AWS). Locally we stub the send.
                logger.LogInformation(
                    "Crawl alert ({Provider}) stub send alert_code={AlertCode} job={JobId} to_configured=true",
                    provider,
                    alertCode,
                    incident.JobId);
                return true;
            }

            if (string.IsNullOrWhiteSpace(opts.SmtpHost))
            {
                logger.LogWarning("Crawl SMTP alert skipped: SmtpHost missing");
                return false;
            }

            using var message = new MailMessage(opts.FromAddress, opts.ToAddress, subject, body);
            using var client = new SmtpClient(opts.SmtpHost, opts.SmtpPort)
            {
                EnableSsl = opts.SmtpEnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(opts.SmtpUser))
            {
                client.Credentials = new NetworkCredential(opts.SmtpUser, opts.SmtpPassword ?? "");
            }

            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Crawl SMTP alert sent alert_code={AlertCode} job={JobId}", alertCode, incident.JobId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Crawl alert send failed alert_code={AlertCode}", alertCode);
            return false;
        }
    }
}
