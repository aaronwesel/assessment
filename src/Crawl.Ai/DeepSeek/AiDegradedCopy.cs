namespace Crawl.Ai.DeepSeek;

/// <summary>Operator-facing degrade messages — no raw environment variable names.</summary>
public static class AiDegradedCopy
{
    public const string OfflineHeuristics = "Assistive AI is offline — using labeled heuristics.";
    public const string OfflineHeuristicAnswer = "Assistive AI is offline — heuristic answer below.";
    public const string OfflineHeuristicSummary = "Assistive AI is offline — heuristic summary below.";
    public const string OfflineHeuristicSuggestions = "Assistive AI is offline — heuristic suggestions only.";
    public const string OfflinePolicy = "Assistive AI is offline — using runbook policy.";

    public static string HttpFallback(int status) =>
        $"Assistive AI unavailable (HTTP {status}) — labeled heuristics.";

    public const string EmptyContent = "Assistive AI returned no text — labeled heuristics.";
    public const string Unavailable = "Assistive AI unavailable — labeled heuristics.";
}
