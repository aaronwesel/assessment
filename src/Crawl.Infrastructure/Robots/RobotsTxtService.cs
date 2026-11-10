using System.Collections.Concurrent;
using Crawl.Domain.Links;
using Crawl.Domain.Robots;
using Crawl.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Robots;

public interface IRobotsTxtService
{
    Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken);
    Task<TimeSpan?> GetCrawlDelayAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// Fetches and caches robots.txt per origin. Fail-open on network errors (crawl continues).
/// </summary>
public sealed class RobotsTxtService(
    IHttpClientFactory httpClientFactory,
    IOptions<CrawlHttpOptions> options,
    ILogger<RobotsTxtService> logger) : IRobotsTxtService
{
    private readonly ConcurrentDictionary<string, CachedRobots> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!options.Value.RespectRobotsTxt)
            return true;

        var rules = await GetRulesAsync(url, cancellationToken);
        return rules.IsAllowed(options.Value.UserAgentToken, url);
    }

    public async Task<TimeSpan?> GetCrawlDelayAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!options.Value.RespectRobotsTxt)
            return null;

        var rules = await GetRulesAsync(url, cancellationToken);
        return rules.GetCrawlDelay(options.Value.UserAgentToken);
    }

    private async Task<RobotsTxtRules> GetRulesAsync(Uri pageUrl, CancellationToken cancellationToken)
    {
        if (!UrlNormalizer.TryNormalize($"{pageUrl.Scheme}://{pageUrl.IdnHost}/robots.txt", out var robotsUri))
            return RobotsTxtRules.Empty;

        var origin = $"{robotsUri.Scheme}://{robotsUri.Authority}".ToLowerInvariant();
        if (_cache.TryGetValue(origin, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Rules;

        var rules = await FetchAsync(robotsUri, cancellationToken);
        _cache[origin] = new CachedRobots(rules, DateTimeOffset.UtcNow.AddMinutes(30));
        return rules;
    }

    private async Task<RobotsTxtRules> FetchAsync(Uri robotsUri, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("crawl-robots");
            using var response = await client.GetAsync(robotsUri, cancellationToken);
            if ((int)response.StatusCode == 404)
                return RobotsTxtRules.Empty;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("robots.txt {Status} for {Url}; fail-open", (int)response.StatusCode, robotsUri);
                return RobotsTxtRules.Empty;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return RobotsTxtRules.Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            logger.LogDebug(ex, "robots.txt fetch failed for {Url}; fail-open", robotsUri);
            return RobotsTxtRules.Empty;
        }
    }

    private sealed record CachedRobots(RobotsTxtRules Rules, DateTimeOffset ExpiresAt);
}
