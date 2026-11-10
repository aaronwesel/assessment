namespace Crawl.Ai.Options;

/// <summary>
/// Feature-flag friendly AI overlay settings. Bound from configuration section "CrawlAi".
/// </summary>
public sealed class CrawlAiOptions
{
    public const string SectionName = "CrawlAi";

    /// <summary>Master kill-switch for all assistive AI endpoints.</summary>
    public bool Enabled { get; set; } = true;

    public bool FieldFinderEnabled { get; set; } = true;
    public bool LoginAssistEnabled { get; set; } = true;
    public bool SummarizationEnabled { get; set; } = true;
    public bool AskTheCrawlEnabled { get; set; } = true;
    public bool AgentLoopEnabled { get; set; } = true;

    /// <summary>
    /// DeepSeek API key. Prefer env DEEPSEEK_API_KEY; when empty, clients degrade gracefully.
    /// </summary>
    public string? DeepSeekApiKey { get; set; }

    public string DeepSeekBaseUrl { get; set; } = "https://api.deepseek.com";
    public string DeepSeekModel { get; set; } = "deepseek-chat";

    /// <summary>Max characters of page HTML/text sent to the model (PHI-minded truncation).</summary>
    public int MaxContentChars { get; set; } = 24_000;
}
