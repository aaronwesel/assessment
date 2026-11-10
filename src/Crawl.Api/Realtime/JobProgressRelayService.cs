using System.Text;
using System.Text.Json;
using Crawl.Domain.Messaging;
using Crawl.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Crawl.Api.Realtime;

/// <summary>
/// Consumes <c>crawl.job.events</c> and pushes <c>jobProgress</c> to SignalR job groups.
/// </summary>
public sealed class JobProgressRelayService(
    RabbitMqConnectionFactory connectionFactory,
    IHubContext<CrawlJobHub> hub,
    IOptions<RabbitMqOptions> options,
    ILogger<JobProgressRelayService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private IModel? _channel;
    private string? _queueName;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = connectionFactory.CreateChannel();
        _queueName = $"{options.Value.ProgressQueuePrefix}.{Environment.MachineName}.{Guid.NewGuid():N}";
        _channel.QueueDeclare(
            _queueName,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null);
        _channel.QueueBind(_queueName, options.Value.ProgressExchange, routingKey: string.Empty);
        _channel.BasicQos(0, 32, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(_queueName, autoAck: false, consumer);

        logger.LogInformation(
            "SignalR progress relay bound to {Exchange} via {Queue}",
            options.Value.ProgressExchange,
            _queueName);

        stoppingToken.Register(() =>
        {
            try { _channel?.Close(); }
            catch (Exception ex) { logger.LogDebug(ex, "Progress relay channel close"); }
        });

        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = _channel ?? throw new InvalidOperationException("Channel not initialized.");
        try
        {
            var progress = JsonSerializer.Deserialize<CrawlJobProgressEvent>(
                Encoding.UTF8.GetString(args.Body.Span),
                JsonOptions);

            if (progress is not null)
            {
                await hub.Clients
                    .Group(CrawlJobHub.GroupName(progress.JobId))
                    .SendAsync(
                        "jobProgress",
                        new
                        {
                            jobId = progress.JobId,
                            status = progress.Status,
                            progressPercent = progress.ProgressPercent,
                            pageCounts = new
                            {
                                total = progress.TotalPages,
                                queued = progress.Queued,
                                processing = progress.Processing,
                                done = progress.Done,
                                skipped = progress.Skipped,
                                failed = progress.Failed
                            },
                            atUtc = progress.AtUtc
                        },
                        CancellationToken.None);
            }

            channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to relay crawl progress to SignalR");
            try { channel.BasicAck(args.DeliveryTag, multiple: false); }
            catch { /* ignore */ }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
