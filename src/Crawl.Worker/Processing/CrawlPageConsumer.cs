using System.Text;
using System.Text.Json;
using Crawl.Ai.Incidents;
using Crawl.Domain.Fetching;
using Crawl.Domain.Incidents;
using Crawl.Domain.Messaging;
using Crawl.Infrastructure.Messaging;
using Crawl.Worker.Metrics;
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
        // Prefetch bounds in-flight messages; MaxConcurrentFetches further caps HTTP.
        _channel.BasicQos(0, (ushort)Math.Max(1, options.Value.PrefetchCount), false);

        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(options.Value.PageQueue, autoAck: false, _consumer);

        logger.LogInformation(
            "Listening on queue {Queue} (prefetch={Prefetch}, depthPriority={DepthPriority})",
            options.Value.PageQueue,
            options.Value.PrefetchCount,
            options.Value.EnableDepthPriority);

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
                CrawlWorkerMetrics.PoisonMessages.Add(1);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<CrawlPageProcessor>();
            // Redelivered=true after crash/unacked so Processing pages can be reclaimed safely.
            await processor.ProcessAsync(message, args.Redelivered, CancellationToken.None);

            channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            var retryCount = GetRetryCount(args.BasicProperties);
            var maxRetries = options.Value.MaxRetryAttempts;
            var correlationId = args.BasicProperties.MessageId
                ?? $"{message?.JobId:N}:{message?.PageId}:{retryCount}";

            await TryReportFaultAsync(message, ex, retryCount + 1, correlationId, exhausted: retryCount >= maxRetries);

            if (retryCount >= maxRetries)
            {
                logger.LogError(
                    ex,
                    "Exhausted retries ({RetryCount}) for JobId={JobId} PageId={PageId}; routing to DLQ",
                    retryCount,
                    message?.JobId,
                    message?.PageId);
                CrawlWorkerMetrics.PoisonMessages.Add(1);
                channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            CrawlWorkerMetrics.TransientRetries.Add(1);
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
            props.Priority = args.BasicProperties.Priority;
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
                "Poison/non-transient failure for JobId={JobId} PageId={PageId}; routing to DLQ",
                message?.JobId,
                message?.PageId);
            CrawlWorkerMetrics.PoisonMessages.Add(1);
            if (message is not null)
            {
                await TryReportFaultAsync(
                    message,
                    ex,
                    attempt: GetRetryCount(args.BasicProperties) + 1,
                    correlationId: args.BasicProperties.MessageId ?? $"{message.JobId:N}:{message.PageId}:poison",
                    exhausted: true);
            }

            channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private async Task TryReportFaultAsync(
        CrawlPageRequested? message,
        Exception ex,
        int attempt,
        string correlationId,
        bool exhausted)
    {
        if (message is null)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<ICrawlIncidentMonitor>();

            CrawlFaultEvent fault;
            if (ex is TransientCrawlException transient)
            {
                var code = exhausted && attempt > 1
                    ? CrawlFaultCode.RepeatedDlq
                    : transient.FaultCode
                      ?? CrawlFaultClassifier.ClassifyException(message.JobId, correlationId, ex, transient.Host, message.PageId, attempt).Code;

                if (exhausted && transient.FaultCode is CrawlFaultCode.HardBlock or CrawlFaultCode.CaptchaOrBlock)
                    code = transient.FaultCode.Value;

                var severity = CrawlFaultClassifier.ClassifySeverity(code, attempt, transient.RetryAfterSeconds);
                fault = new CrawlFaultEvent(
                    message.JobId,
                    correlationId,
                    code,
                    severity,
                    CrawlFaultClassifier.SanitizeSummary(transient.Message),
                    transient.Host,
                    message.PageId,
                    transient.HttpStatus,
                    transient.RetryAfterSeconds,
                    attempt);
            }
            else
            {
                fault = exhausted
                    ? new CrawlFaultEvent(
                        message.JobId,
                        correlationId,
                        CrawlFaultCode.RepeatedDlq,
                        CrawlFaultSeverity.Error,
                        CrawlFaultClassifier.SanitizeSummary($"DLQ after transient failures: {ex.GetType().Name}"),
                        PageId: message.PageId,
                        Attempt: attempt)
                    : CrawlFaultClassifier.ClassifyException(
                        message.JobId,
                        correlationId,
                        ex,
                        pageId: message.PageId,
                        attempt: attempt);
            }

            await monitor.HandleAsync(fault, CancellationToken.None);
        }
        catch (Exception reportEx)
        {
            logger.LogWarning(reportEx, "Failed to report crawl incident for JobId={JobId}", message.JobId);
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
        ex is TransientCrawlException
            or HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or IOException
            or BrokerUnreachableException
            || ex.InnerException is TransientCrawlException or HttpRequestException or TaskCanceledException;

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
