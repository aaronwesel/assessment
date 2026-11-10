using Crawl.Domain.Messaging;

namespace Crawl.Infrastructure.Messaging;

public interface ICrawlProgressPublisher
{
    Task PublishAsync(CrawlJobProgressEvent progress, CancellationToken cancellationToken = default);
}
