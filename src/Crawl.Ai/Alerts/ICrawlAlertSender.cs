using Crawl.Domain.Incidents;

namespace Crawl.Ai.Alerts;

public interface ICrawlAlertSender
{
    Task<bool> TrySendIncidentAlertAsync(
        CrawlIncident incident,
        string alertCode,
        CancellationToken cancellationToken = default);
}
