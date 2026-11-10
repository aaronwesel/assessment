using Crawl.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Health;

/// <summary>
/// Readiness probe: AMQP connection against configured RabbitMQ.
/// </summary>
public sealed class RabbitMqConnectivityHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
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

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclarePassive(opts.ProgressExchange);
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ reachable"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connectivity failed", ex));
        }
    }
}
