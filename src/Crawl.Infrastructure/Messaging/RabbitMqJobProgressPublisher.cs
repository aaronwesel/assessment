using System.Text;
using System.Text.Json;
using Crawl.Domain.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

public sealed class RabbitMqJobProgressPublisher(
    RabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqJobProgressPublisher> logger) : IJobProgressPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private IModel? _channel;

    public Task PublishAsync(CrawlJobProgressEvent progress, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channel = GetChannel();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(progress, JsonOptions));
        var props = channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        props.Type = "crawl.job.progress";
        props.MessageId = $"{progress.JobId:N}:{progress.EventKind}:{progress.OccurredAt.UtcTicks}";

        channel.BasicPublish(
            exchange: connectionFactory.Options.JobEventsExchange,
            routingKey: string.Empty,
            basicProperties: props,
            body: body);

        logger.LogDebug(
            "Published job progress JobId={JobId} Kind={Kind} Status={Status} Progress={Progress}",
            progress.JobId,
            progress.EventKind,
            progress.Status,
            progress.ProgressPercent);
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
