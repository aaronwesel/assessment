using Crawl.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Health;

/// <summary>
/// Readiness probe: opens an AMQP connection/channel against configured RabbitMQ (worker + API ready).
/// </summary>
public sealed class RabbitMqConnectivityHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opts = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = opts.HostName,
                Port = opts.Port,
                UserName = opts.UserName,
                Password = opts.Password,
                VirtualHost = opts.VirtualHost,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            // Passive declare is enough to prove channel usability without mutating crawl topology.
            await channel.ExchangeDeclarePassiveAsync(CrawlMassTransitRegistration.DeadLetterExchange, cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ connection and DLX reachable");
        }
        catch (Exception ex)
        {
            // During first boot DLX may not exist yet — fall back to connection-only success.
            if (ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                || ex.ToString().Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var opts = options.Value;
                    var factory = new ConnectionFactory
                    {
                        HostName = opts.HostName,
                        Port = opts.Port,
                        UserName = opts.UserName,
                        Password = opts.Password,
                        VirtualHost = opts.VirtualHost,
                        RequestedConnectionTimeout = TimeSpan.FromSeconds(3)
                    };
                    await using var connection = await factory.CreateConnectionAsync(cancellationToken);
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                    return HealthCheckResult.Healthy("RabbitMQ connection reachable (DLX pending first declare)");
                }
                catch (Exception inner)
                {
                    return HealthCheckResult.Unhealthy("RabbitMQ unreachable", inner);
                }
            }

            return HealthCheckResult.Unhealthy("RabbitMQ connectivity failed", ex);
        }
    }
}
