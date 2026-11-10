using Crawl.Ai.Agent;
using Crawl.Ai.Alerts;
using Crawl.Ai.AskTheCrawl;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Incidents;
using Crawl.Ai.Integrations;
using Crawl.Ai.LoginAssist;
using Crawl.Ai.Options;
using Crawl.Ai.SetupGuide;
using Crawl.Ai.Summarization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crawl.Ai;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers assistive AI overlays. Crawl.Api should call this additively:
    /// <c>builder.Services.AddCrawlAi(builder.Configuration);</c>
    /// Also safe on Crawl.Worker for incident monitor remediations.
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

        services.AddOptions<CrawlAlertOptions>()
            .Bind(configuration.GetSection(CrawlAlertOptions.SectionName))
            .PostConfigure(opts =>
            {
                // Mirror platform SES env knobs when crawl-specific values are empty.
                opts.EmailEnabled = opts.EmailEnabled
                    || string.Equals(Environment.GetEnvironmentVariable("Crawl__Alerts__EmailEnabled"), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("CRAWL_ALERTS_EMAIL_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(opts.FromAddress))
                    opts.FromAddress = Environment.GetEnvironmentVariable("SES_FROM_EMAIL");
                if (string.IsNullOrWhiteSpace(opts.ToAddress))
                    opts.ToAddress = Environment.GetEnvironmentVariable("CRAWL_ALERT_TO_EMAIL")
                        ?? Environment.GetEnvironmentVariable("LOGIN_ALERT_TO_EMAIL");
                if (string.IsNullOrWhiteSpace(opts.SesRegion))
                    opts.SesRegion = Environment.GetEnvironmentVariable("SES_REGION")
                        ?? Environment.GetEnvironmentVariable("AWS_REGION");
                if (string.IsNullOrWhiteSpace(opts.SmtpPassword))
                    opts.SmtpPassword = Environment.GetEnvironmentVariable("Crawl__Alerts__SmtpPassword");

                if (string.Equals(Environment.GetEnvironmentVariable("SES_ENABLED"), "true", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(opts.Provider, "smtp", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(opts.SmtpHost))
                {
                    opts.Provider = "ses";
                }
            });

        services.AddHttpClient("crawl-setup-guide", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TrailmapCrawl-SetupGuide/1.0");
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
        services.AddScoped<ISetupGuideService, SetupGuideService>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        services.AddScoped<IPayerPortalClient, StubPayerPortalClient>();
        services.AddScoped<IClearinghouseClient, StubClearinghouseClient>();
        services.AddScoped<IRemediationAgent, RemediationAgent>();
        services.AddScoped<ICrawlIncidentMonitor, CrawlIncidentMonitor>();
        services.AddSingleton<ICrawlAlertSender, CrawlAlertSender>();

        return services;
    }
}
