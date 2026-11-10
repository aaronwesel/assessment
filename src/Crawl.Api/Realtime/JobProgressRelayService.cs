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
/// Consumes <c>crawl.job.events</c> fanout and pushes <c>jobProgress</c> to SignalR job groups.
/// </summary>
public sealed class JobProgressRelayService(
    RabbitMqConnectionFactory connectionFactory,
    IHubContext<CrawlJobsHub> hub,
    IOptions<RabbitMqOptions> options,
    ILogger<JobProgressRelayService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry loop so API can boot before RabbitMQ is ready.
        while (!stoppingToken.IsCancellationRequested)
        {
            IModel? channel = null;
            try
            {
                channel = connectionFactory.CreateChannel();
                var exchange = options.Value.JobEventsExchange;
                channel.ExchangeDeclare(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);

                var queue = channel.QueueDeclare(
                    queue: string.Empty,
                    durable: false,
                    exclusive: true,
                    autoDelete: true).QueueName;
                channel.QueueBind(queue, exchange, routingKey: string.Empty);
                channel.BasicQos(0, 32, false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, args) =>
                {
                    try
                    {
                        var json = Encoding.UTF8.GetString(args.Body.ToArray());
                        var progress = JsonSerializer.Deserialize<CrawlJobProgressEvent>(json, JsonOptions);
                        if (progress is null)
                        {
                            channel.BasicAck(args.DeliveryTag, false);
                            return;
                        }

                        await hub.Clients
                            .Group(CrawlJobsHub.GroupName(progress.JobId))
                            .SendAsync("jobProgress", progress, stoppingToken)
                            .ConfigureAwait(false);

                        channel.BasicAck(args.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to relay job progress event");
                        try
                        {
                            channel.BasicNack(args.DeliveryTag, false, requeue: false);
                        }
                        catch
                        {
                            // Channel may already be closing.
                        }
                    }
                };

                channel.BasicConsume(queue, autoAck: false, consumer);
                logger.LogInformation(
                    "Job progress SignalR relay listening on exchange {Exchange} queue {Queue}",
                    exchange,
                    queue);

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Job progress relay disconnected; retrying in 3s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                try
                {
                    channel?.Close();
                    channel?.Dispose();
                }
                catch
                {
                    // ignore dispose races on shutdown
                }
            }
        }
    }
}
