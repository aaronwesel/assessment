using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
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

        var httpOptions = configuration.GetSection(CrawlHttpOptions.SectionName).Get<CrawlHttpOptions>()
            ?? new CrawlHttpOptions();

        services.AddHttpClient("crawl")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(httpOptions.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CrawlAssessmentBot/1.0 (+https://github.com/aaronwesel/assessment)");
            })
            .AddResilienceHandler("crawl-http", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = httpOptions.MaxRetryAttempts,
                    Delay = TimeSpan.FromMilliseconds(250),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Result is { IsSuccessStatusCode: false, StatusCode: >= System.Net.HttpStatusCode.InternalServerError }
                        || args.Outcome.Exception is HttpRequestException or TaskCanceledException)
                });
            });

        return services;
    }
}
