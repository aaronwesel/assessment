using System.Text;
using System.Text.Json;
using Crawl.Domain.Messaging;
using Crawl.Domain.Scheduling;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

public sealed class RabbitMqCrawlPublisher(
    RabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqCrawlPublisher> logger) : ICrawlMessagePublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private IModel? _channel;

    public Task PublishAsync(CrawlPageRequested message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channel = GetChannel();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";
        props.MessageId = $"{message.JobId}:{message.PageId}";
        props.Headers = new Dictionary<string, object>
        {
            ["x-retry-count"] = 0,
            ["x-crawl-depth"] = message.Depth
        };

        if (connectionFactory.Options.EnableDepthPriority)
            props.Priority = DepthPriority.ForDepth(message.Depth);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: connectionFactory.Options.PageQueue,
            basicProperties: props,
            body: body);

        logger.LogInformation(
            "Published crawl.page JobId={JobId} PageId={PageId} Depth={Depth}",
            message.JobId,
            message.PageId,
            message.Depth);

        return Task.CompletedTask;
    }

    private IModel GetChannel()
    {
        lock (_gate)
        {
            if (_channel is { IsOpen: true })
                return _channel;

            _channel?.Dispose();
            _channel = connectionFactory.CreateChannel();
            return _channel;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _channel?.Dispose();
            _channel = null;
        }
    }
}
