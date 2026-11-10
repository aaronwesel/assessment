using Crawl.Domain.Messaging;

namespace Crawl.Infrastructure.Messaging;

public interface ICrawlMessagePublisher
{
    Task PublishAsync(CrawlPageRequested message, CancellationToken cancellationToken = default);
}
