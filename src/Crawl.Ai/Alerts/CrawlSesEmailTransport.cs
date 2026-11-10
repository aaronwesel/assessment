using System.Net;
using System.Net.Mail;
using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Crawl.Ai.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.Alerts;

/// <summary>
/// Shared SMTP / Amazon SES send path. Mirrors platform prep (<c>SES_ENABLED</c>, <c>SES_FROM_EMAIL</c>, task role).
/// Never logs recipients or bodies (HIPAA).
/// </summary>
public interface ICrawlSesEmailTransport
{
    bool IsReady { get; }

    Task<bool> TrySendAsync(
        string toAddress,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default);
}

public sealed class CrawlSesEmailTransport(
    IOptions<CrawlAlertOptions> options,
    ILogger<CrawlSesEmailTransport> logger) : ICrawlSesEmailTransport
{
    public bool IsReady
    {
        get
        {
            var opts = options.Value;
            if (!opts.EmailEnabled || string.IsNullOrWhiteSpace(opts.FromAddress))
                return false;

            var provider = (opts.Provider ?? "smtp").Trim().ToLowerInvariant();
            return provider switch
            {
                "stub" => true,
                "ses" => SesEnabled(),
                "smtp" => !string.IsNullOrWhiteSpace(opts.SmtpHost),
                _ => false
            };
        }
    }

    public async Task<bool> TrySendAsync(
        string toAddress,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.EmailEnabled)
        {
            logger.LogDebug("Crawl email skipped: EmailEnabled=false");
            return false;
        }

        if (string.IsNullOrWhiteSpace(opts.FromAddress) || string.IsNullOrWhiteSpace(toAddress))
        {
            logger.LogDebug("Crawl email skipped: from/to missing");
            return false;
        }

        var provider = (opts.Provider ?? "smtp").Trim().ToLowerInvariant();

        try
        {
            if (provider is "stub")
            {
                logger.LogInformation(
                    "Crawl email stub send provider=stub has_html=false");
                return true;
            }

            if (provider is "ses")
            {
                if (!SesEnabled())
                {
                    logger.LogInformation("Crawl SES send skipped: SES_ENABLED not true or from-address missing");
                    return false;
                }

                return await SendViaSesAsync(opts, toAddress, subject, bodyText, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(opts.SmtpHost))
            {
                logger.LogWarning("Crawl SMTP send skipped: SmtpHost missing");
                return false;
            }

            using var message = new MailMessage(opts.FromAddress, toAddress, subject, bodyText);
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
            logger.LogInformation("Crawl SMTP email sent");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Crawl email send failed");
            return false;
        }
    }

    private static bool SesEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("SES_ENABLED"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> SendViaSesAsync(
        CrawlAlertOptions opts,
        string toAddress,
        string subject,
        string bodyText,
        CancellationToken cancellationToken)
    {
        var regionName = opts.SesRegion
            ?? Environment.GetEnvironmentVariable("SES_REGION")
            ?? Environment.GetEnvironmentVariable("AWS_REGION");
        if (string.IsNullOrWhiteSpace(regionName))
        {
            logger.LogInformation("Crawl SES send skipped: SES_REGION / AWS_REGION not set");
            return false;
        }

        var region = RegionEndpoint.GetBySystemName(regionName);
        using var client = new AmazonSimpleEmailServiceClient(region);

        var request = new SendEmailRequest
        {
            Source = opts.FromAddress,
            Destination = new Destination { ToAddresses = [toAddress] },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body { Text = new Content(bodyText) }
            }
        };

        var response = await client.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.MessageId;
        return true;
    }
}
