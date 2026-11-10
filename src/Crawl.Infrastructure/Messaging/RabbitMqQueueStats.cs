using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Live RabbitMQ depth / consumer signals for local scale-out and ECS autoscaling hooks.
/// Uses passive declare (no Management plugin dependency).
/// </summary>
public sealed class RabbitMqQueueStats(
    RabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqQueueStats> logger)
{
    private readonly object _gate = new();
    private IModel? _channel;

    public QueueDepthSnapshot ReadPageQueue()
    {
        try
        {
            var channel = GetChannel();
            var result = channel.QueueDeclarePassive(options.Value.PageQueue);
            uint dlqDepth = 0;
            try
            {
                var dlq = channel.QueueDeclarePassive(options.Value.PageDlq);
                dlqDepth = dlq.MessageCount;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "DLQ passive declare failed");
            }

            return new QueueDepthSnapshot(
                options.Value.PageQueue,
                result.MessageCount,
                result.ConsumerCount,
                dlqDepth,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read queue depth for {Queue}", options.Value.PageQueue);
            return new QueueDepthSnapshot(
                options.Value.PageQueue,
                MessagesReady: 0,
                ConsumerCount: 0,
                DlqMessages: 0,
                AtUtc: DateTimeOffset.UtcNow,
                Healthy: false,
                Error: "queue_stats_unavailable");
        }
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
}

public sealed record QueueDepthSnapshot(
    string Queue,
    uint MessagesReady,
    uint ConsumerCount,
    uint DlqMessages,
    DateTimeOffset AtUtc,
    bool Healthy = true,
    string? Error = null)
{
    /// <summary>
    /// Suggests whether operators / ASG should add workers (depth high relative to consumers).
    /// </summary>
    public bool SuggestScaleOut(uint depthPerConsumer = 20) =>
        Healthy
        && MessagesReady > 0
        && (ConsumerCount == 0 || MessagesReady >= depthPerConsumer * Math.Max(1u, ConsumerCount));

    public bool SuggestScaleIn(uint idleThreshold = 0) =>
        Healthy && MessagesReady <= idleThreshold && ConsumerCount > 1;
}
