using Crawl.Domain.Messaging;
using Crawl.Domain.Scheduling;
using Crawl.Infrastructure.Observability;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Publishes <see cref="CrawlPageRequested"/> to the <c>crawl.page</c> exchange (Worker binds the queue).
/// Avoids API re-declaring the work queue with mismatched DLX arguments.
/// </summary>
public sealed class MassTransitCrawlPublisher(
    IPublishEndpoint publishEndpoint,
    IOptions<RabbitMqOptions> options,
    ILogger<MassTransitCrawlPublisher> logger) : ICrawlMessagePublisher
{
    public async Task PublishAsync(CrawlPageRequested message, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var correlationId = CorrelationId.Current
            ?? $"{message.JobId:N}:{message.PageId}";

        await publishEndpoint.Publish(message, context =>
        {
            context.CorrelationId = Guid.TryParse(correlationId, out var cid) ? cid : message.JobId;
            context.Headers.Set(CorrelationId.HeaderName, correlationId);
            context.Headers.Set("x-crawl-depth", message.Depth);
            context.Headers.Set("JobId", message.JobId.ToString("D"));

            if (opts.EnableDepthPriority)
                context.SetPriority(DepthPriority.ForDepth(message.Depth));
        }, cancellationToken);

        logger.LogInformation(
            "Published crawl.page via MassTransit JobId={JobId} PageId={PageId} Depth={Depth} CorrelationId={CorrelationId}",
            message.JobId,
            message.PageId,
            message.Depth,
            correlationId);
    }
}
