using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// Shared RabbitMQ connection + channel factory. Declares crawl topology on every new channel:
/// primary queue <c>crawl.page</c> with dead-letter to fanout <c>crawl.page.dlx</c> → <c>crawl.page.dlq</c>.
/// At-least-once: workers ack after successful handle; nack without requeue after poison or exhausted retries.
/// </summary>
public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options) : IDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly object _gate = new();
    private IConnection? _connection;

    public RabbitMqOptions Options => _options;

    public IConnection GetConnection()
    {
        lock (_gate)
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _connection?.Dispose();
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            _connection = factory.CreateConnection("crawl");
            return _connection;
        }
    }

    public IModel CreateChannel()
    {
        var channel = GetConnection().CreateModel();
        DeclareTopology(channel);
        return channel;
    }

    public void DeclareTopology(IModel channel)
    {
        // Fanout DLX: any nack/reject from crawl.page routes poison/exhausted messages here.
        channel.ExchangeDeclare("crawl.page.dlx", ExchangeType.Fanout, durable: true, autoDelete: false);

        channel.QueueDeclare(
            _options.PageDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_options.PageDlq, "crawl.page.dlx", routingKey: string.Empty);

        // x-dead-letter-exchange: broker moves nacked messages to DLX (no infinite BasicNack requeue loops).
        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = "crawl.page.dlx"
        };

        // Optional BFS: shallower depth → higher AMQP priority (see DepthPriority).
        if (_options.EnableDepthPriority)
            args["x-max-priority"] = (int)_options.MaxDepthPriority;

        channel.QueueDeclare(
            _options.PageQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: args);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
