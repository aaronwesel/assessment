namespace Crawl.Ai.DeepSeek;

public interface IDeepSeekClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Chat completion. Returns null content with a degraded reason when the key is missing or the call fails.
    /// </summary>
    Task<DeepSeekResult> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

public sealed record DeepSeekResult(string? Content, bool UsedModel, string? DegradedReason);
