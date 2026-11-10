namespace Crawl.Infrastructure.Http;

public sealed class CrawlHttpOptions
{
    public const string SectionName = "CrawlHttp";

    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Max response body bytes (HTML only).</summary>
    public int MaxResponseBytes { get; set; } = 2_000_000;

    /// <summary>Global concurrent in-flight HTTP fetches per worker process.</summary>
    public int MaxConcurrentFetches { get; set; } = 4;

    /// <summary>Default inter-request delay per host when robots has no Crawl-delay.</summary>
    public int PolitenessDelayMilliseconds { get; set; } = 250;

    /// <summary>Honor robots.txt Disallow / Crawl-delay when true.</summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>User-Agent token matched against robots.txt groups.</summary>
    public string UserAgentToken { get; set; } = "CrawlAssessmentBot";

    /// <summary>Full User-Agent header sent on fetches.</summary>
    public string UserAgent { get; set; } =
        "CrawlAssessmentBot/1.0 (+https://github.com/aaronwesel/assessment)";

    /// <summary>Max HTTP redirects to follow (loop-guarded).</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>Sort query parameters during URL normalization for stronger dedup.</summary>
    public bool SortQueryParameters { get; set; } = true;
}
