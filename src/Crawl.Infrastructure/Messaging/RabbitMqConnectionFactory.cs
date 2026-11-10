using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Crawl.Infrastructure.Messaging;

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
        channel.ExchangeDeclare("crawl.page.dlx", ExchangeType.Fanout, durable: true, autoDelete: false);

        channel.QueueDeclare(
            _options.PageDlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_options.PageDlq, "crawl.page.dlx", routingKey: string.Empty);

        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = "crawl.page.dlx"
        };

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
