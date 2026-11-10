using System.Text;
using System.Text.Json;
using Crawl.Domain.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Publishes job progress to fanout <c>crawl.job.events</c> for API SignalR relay.
/// </summary>
public sealed class RabbitMqProgressPublisher(
    RabbitMqConnectionFactory connectionFactory,
    ILogger<RabbitMqProgressPublisher> logger) : ICrawlProgressPublisher, IDisposable
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
        props.Persistent = false;
        props.ContentType = "application/json";
        props.MessageId = $"{progress.JobId:N}:{progress.AtUtc.ToUnixTimeMilliseconds()}";
        props.Headers = new Dictionary<string, object>
        {
            ["x-job-id"] = progress.JobId.ToString("D")
        };

        channel.BasicPublish(
            exchange: connectionFactory.Options.ProgressExchange,
            routingKey: string.Empty,
            basicProperties: props,
            body: body);

        logger.LogDebug(
            "Published crawl progress JobId={JobId} Status={Status} Pct={Pct}",
            progress.JobId,
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
