using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Declares dedicated DLX <c>crawl.page.dlx</c> and durable DLQ <c>crawl.page.dlq</c> once at Worker start.
/// Keeps poison messages out of a MassTransit receive endpoint (inspect/replay only).
/// </summary>
public sealed class CrawlTopologyBootstrapHostedService(
    IOptions<RabbitMqOptions> options,
    ILogger<CrawlTopologyBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = opts.HostName,
            Port = opts.Port,
            UserName = opts.UserName,
            Password = opts.Password,
            VirtualHost = opts.VirtualHost
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            CrawlMassTransitRegistration.DeadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            opts.PageDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            opts.PageDlq,
            CrawlMassTransitRegistration.DeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Declared crawl DLX {Exchange} → DLQ {Queue}",
            CrawlMassTransitRegistration.DeadLetterExchange,
            opts.PageDlq);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
