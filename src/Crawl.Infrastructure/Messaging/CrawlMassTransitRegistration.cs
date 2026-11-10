using Crawl.Domain.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Messaging;

/// <summary>
/// MassTransit + RabbitMQ for Crawl.Api (publish) and Crawl.Worker (consume).
/// API publishes to exchange <c>crawl.page</c>; Worker owns queue topology + DLX
/// <c>crawl.page.dlx</c> → <c>crawl.page.dlq</c>. Retries use
/// <c>UseMessageRetry(r => r.Exponential(...))</c>.
/// </summary>
public static class CrawlMassTransitRegistration
{
    public const string DeadLetterExchange = "crawl.page.dlx";
    public const string PageExchange = "crawl.page";

    public static IServiceCollection AddCrawlMassTransitPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureHost(context, cfg);
                cfg.Message<CrawlPageRequested>(m => m.SetEntityName(PageExchange));
            });
        });
        services.AddScoped<ICrawlMessagePublisher, MassTransitCrawlPublisher>();
        return services;
    }

    public static IServiceCollection AddCrawlMassTransitWorker<TConsumer>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TConsumer : class, IConsumer<CrawlPageRequested>
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddMassTransit(x =>
        {
            x.AddConsumer<TConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var opts = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                ConfigureHost(context, cfg);
                cfg.Message<CrawlPageRequested>(m => m.SetEntityName(PageExchange));

                // Work queue owns topology; DLX/DLQ are declared by CrawlTopologyBootstrapHostedService
                // (no ReceiveEndpoint on the DLQ — operators inspect/replay manually).
                cfg.ReceiveEndpoint(opts.PageQueue, e =>
                {
                    e.ConfigureConsumeTopology = false;
                    e.Durable = true;
                    e.PrefetchCount = (ushort)Math.Max(1, opts.PrefetchCount);
                    e.ConcurrentMessageLimit = Math.Max(1, Math.Min(opts.PrefetchCount, 8));

                    if (opts.EnableDepthPriority)
                        e.SetQueueArgument("x-max-priority", (int)opts.MaxDepthPriority);

                    e.SetQueueArgument("x-dead-letter-exchange", DeadLetterExchange);

                    // Bind work queue to the crawl.page fanout exchange the API publishes to.
                    e.Bind(PageExchange, s =>
                    {
                        s.ExchangeType = "fanout";
                        s.Durable = true;
                    });

                    e.UseMessageRetry(r => r.Exponential(
                        retryLimit: Math.Max(1, opts.MaxRetryAttempts),
                        minInterval: TimeSpan.FromMilliseconds(200),
                        maxInterval: TimeSpan.FromSeconds(30),
                        intervalDelta: TimeSpan.FromMilliseconds(400)));

                    e.ConfigureConsumer<TConsumer>(context);
                });
            });
        });

        services.AddHostedService<CrawlTopologyBootstrapHostedService>();
        services.AddScoped<ICrawlMessagePublisher, MassTransitCrawlPublisher>();
        return services;
    }

    private static void ConfigureHost(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg)
    {
        var opts = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        cfg.Host(opts.HostName, (ushort)opts.Port, opts.VirtualHost, h =>
        {
            h.Username(opts.UserName);
            h.Password(opts.Password);
        });
    }
}
