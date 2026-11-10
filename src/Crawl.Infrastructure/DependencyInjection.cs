using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Infrastructure.Robots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        var connectionString = configuration.GetConnectionString("Crawl")
            ?? throw new InvalidOperationException("Connection string 'Crawl' is required.");

        services.AddDbContext<CrawlDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<ICrawlMessagePublisher, RabbitMqCrawlPublisher>();
        services.AddSingleton<HtmlLinkExtractor>();
        services.AddSingleton<HostPolitenessGate>();
        services.AddSingleton<FetchConcurrencyGate>();
        services.AddSingleton<IRobotsTxtService, RobotsTxtService>();

        var httpOptions = configuration.GetSection(CrawlHttpOptions.SectionName).Get<CrawlHttpOptions>()
            ?? new CrawlHttpOptions();

        // Manual redirect policy + loop guard lives in HtmlPageFetcher.
        services.AddHttpClient("crawl")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All
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

        // Separate client for robots.txt (no resilience retry storm on 404).
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
}
