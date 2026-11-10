using Crawl.Infrastructure.Egress;
using Crawl.Infrastructure.Health;
using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Infrastructure.Remediation;
using Crawl.Infrastructure.Robots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Crawl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrawlInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<CrawlHttpOptions>(configuration.GetSection(CrawlHttpOptions.SectionName));
        services.Configure<CrawlEgressOptions>(configuration.GetSection(CrawlEgressOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Crawl")
            ?? throw new InvalidOperationException("Connection string 'Crawl' is required.");

        services.AddDbContext<CrawlDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<PageUpsertStore>();
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<ICrawlMessagePublisher, RabbitMqCrawlPublisher>();
        services.AddSingleton<ICrawlProgressPublisher, RabbitMqProgressPublisher>();
        services.AddSingleton<IJobProgressPublisher, RabbitMqJobProgressPublisher>();
        services.AddSingleton<RabbitMqQueueStats>();
        services.AddSingleton<HtmlLinkExtractor>();
        services.AddSingleton<HostPolitenessGate>();
        services.AddSingleton<FetchConcurrencyGate>();
        services.AddSingleton<IRobotsTxtService, RobotsTxtService>();
        services.AddSingleton<IEgressProxyProvider, ConfigListEgressProxyProvider>();
        services.AddSingleton<EgressWebProxy>();
        services.AddSingleton<ICrawlRemediationApplicator, CrawlRemediationApplicator>();

        services.AddSingleton(sp =>
        {
            var cs = configuration.GetConnectionString("Crawl")
                ?? throw new InvalidOperationException("Connection string 'Crawl' is required.");
            return Npgsql.NpgsqlDataSource.Create(cs);
        });

        var httpOptions = configuration.GetSection(CrawlHttpOptions.SectionName).Get<CrawlHttpOptions>()
            ?? new CrawlHttpOptions();

        services.AddHttpClient("crawl")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var egressProxy = sp.GetRequiredService<EgressWebProxy>();
                return new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    Proxy = egressProxy,
                    UseProxy = true
                };
            })
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(httpOptions.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", httpOptions.UserAgent);
            })
            .AddResilienceHandler("crawl-http", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = Math.Max(1, httpOptions.MaxRetryAttempts),
                    Delay = TimeSpan.FromMilliseconds(250),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Result is { StatusCode: >= System.Net.HttpStatusCode.InternalServerError }
                        || args.Outcome.Result is { StatusCode: System.Net.HttpStatusCode.RequestTimeout }
                        || args.Outcome.Result is { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
                        || args.Outcome.Exception is HttpRequestException or TaskCanceledException)
                });
            });

        services.AddHttpClient("crawl-robots")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3
            })
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(Math.Min(10, httpOptions.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", httpOptions.UserAgent);
            });

        return services;
    }

    public static IHealthChecksBuilder AddCrawlHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var crawlCs = configuration.GetConnectionString("Crawl")
            ?? throw new InvalidOperationException("ConnectionStrings:Crawl is required.");

        return services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("process up"), tags: ["live"])
            .AddNpgSql(crawlCs, name: "postgres", tags: ["ready"])
            .AddCheck<RabbitMqConnectivityHealthCheck>("rabbitmq", tags: ["ready"]);
    }
}
