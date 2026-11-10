using Crawl.Domain.Messaging;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Publishes job progress to the <c>crawl.job.events</c> fanout for API SignalR relay.
/// </summary>
public interface IJobProgressPublisher
{
    Task PublishAsync(CrawlJobProgressEvent progress, CancellationToken cancellationToken = default);
}
