using System.Text;
using System.Text.Json;
using Crawl.Domain.Messaging;
using Crawl.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Crawl.Worker.Processing;

public sealed class CrawlPageConsumer(
    RabbitMqConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<CrawlPageConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private IModel? _channel;
    private AsyncEventingBasicConsumer? _consumer;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = connectionFactory.CreateChannel();
        _channel.BasicQos(0, (ushort)Math.Max(1, options.Value.PrefetchCount), false);

        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(options.Value.PageQueue, autoAck: false, _consumer);

        logger.LogInformation("Listening on queue {Queue}", options.Value.PageQueue);

        stoppingToken.Register(() =>
        {
            try
            {
                _channel?.Close();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error closing RabbitMQ channel on shutdown");
            }
        });

        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = _channel ?? throw new InvalidOperationException("Channel not initialized.");
        CrawlPageRequested? message = null;

        try
        {
            message = JsonSerializer.Deserialize<CrawlPageRequested>(
                Encoding.UTF8.GetString(args.Body.Span),
                JsonOptions);

            if (message is null)
            {
                logger.LogError("Poison message: null payload; sending to DLQ");
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<CrawlPageProcessor>();
            await processor.ProcessAsync(message, CancellationToken.None);

            channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            var retryCount = GetRetryCount(args.BasicProperties);
            var maxRetries = options.Value.MaxRetryAttempts;

            if (retryCount >= maxRetries)
            {
                logger.LogError(
                    ex,
                    "Exhausted retries ({RetryCount}) for JobId={JobId} PageId={PageId}; routing to DLQ",
                    retryCount,
                    message?.JobId,
                    message?.PageId);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            logger.LogWarning(
                ex,
                "Transient failure retry {RetryCount}/{MaxRetries} JobId={JobId} PageId={PageId}",
                retryCount + 1,
                maxRetries,
                message?.JobId,
                message?.PageId);

            // Republish with incremented retry header, then ack original to avoid infinite requeue loops.
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = args.BasicProperties.MessageId;
            props.Headers = new Dictionary<string, object>(
                args.BasicProperties.Headers?.ToDictionary(k => k.Key, v => v.Value)
                ?? new Dictionary<string, object>())
            {
                ["x-retry-count"] = retryCount + 1
            };

            channel.BasicPublish(string.Empty, options.Value.PageQueue, props, args.Body);
            channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Non-transient failure for JobId={JobId} PageId={PageId}; routing to DLQ",
                message?.JobId,
                message?.PageId);
            channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static int GetRetryCount(IBasicProperties props)
    {
        if (props.Headers is null || !props.Headers.TryGetValue("x-retry-count", out var raw) || raw is null)
            return 0;

        return raw switch
        {
            byte b => b,
            int i => i,
            long l => (int)l,
            byte[] bytes when bytes.Length > 0 => bytes[0],
            _ => int.TryParse(raw.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or IOException
            or BrokerUnreachableException;

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
