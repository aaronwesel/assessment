using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;

namespace Crawl.Ai.Alerts;

public interface IJobSubscriberAlertService
{
    Task TrySendTerminalStatusAlertAsync(CrawlJob job, CancellationToken cancellationToken = default);

    Task TrySendIncidentAlertAsync(
        CrawlJob job,
        CrawlIncident incident,
        string alertCode,
        CancellationToken cancellationToken = default);
}
