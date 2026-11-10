using Crawl.Ai.Agent;
using Crawl.Ai.AskTheCrawl;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Integrations;
using Crawl.Ai.LoginAssist;
using Crawl.Ai.Options;
using Crawl.Ai.Summarization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crawl.Ai;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers assistive AI overlays. Crawl.Api should call this additively:
    /// <c>builder.Services.AddCrawlAi(builder.Configuration);</c>
    /// </summary>
    public static IServiceCollection AddCrawlAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CrawlAiOptions>()
            .Bind(configuration.GetSection(CrawlAiOptions.SectionName))
            .PostConfigure(opts =>
            {
                if (string.IsNullOrWhiteSpace(opts.DeepSeekApiKey))
                {
                    opts.DeepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
                }
            });

        services.AddHttpClient<IDeepSeekClient, DeepSeekClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CrawlAiOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(opts.DeepSeekBaseUrl)
                ? "https://api.deepseek.com"
                : opts.DeepSeekBaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<ILoginAssistService, LoginAssistService>();
        services.AddScoped<IFieldFinderService, FieldFinderService>();
        services.AddScoped<IResultSummarizer, ResultSummarizer>();
        services.AddScoped<IAskTheCrawlService, AskTheCrawlService>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        services.AddScoped<IPayerPortalClient, StubPayerPortalClient>();
        services.AddScoped<IClearinghouseClient, StubClearinghouseClient>();

        return services;
    }
}
