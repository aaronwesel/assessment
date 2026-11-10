namespace Crawl.Ai.Models;

/// <summary>Suggested extraction target for operator confirmation — never auto-applied.</summary>
public sealed record FieldSuggestion(
    string Label,
    string? CssSelector,
    string? XPath,
    string Reason,
    double Confidence,
    string? SampleValue);

public sealed record FieldFinderRequest(
    Guid JobId,
    Guid? PageId,
    string? PageUrl,
    string HtmlOrText,
    string? GoalHint);

public sealed record FieldFinderResponse(
    Guid JobId,
    IReadOnlyList<FieldSuggestion> Suggestions,
    bool UsedModel,
    string? DegradedReason,
    bool RequiresOperatorConfirmation = true);

public sealed record LoginFormField(string Name, string Type, string? Label, bool IsPassword);

public sealed record LoginFormDetection(
    bool Detected,
    string? FormAction,
    string? FormMethod,
    IReadOnlyList<LoginFormField> Fields,
    IReadOnlyList<string> GuidedSteps);

public sealed record LoginAssistDetectRequest(
    Guid JobId,
    string? PageUrl,
    string HtmlOrText);

public sealed record LoginAssistDetectResponse(
    Guid JobId,
    LoginFormDetection Detection,
    string AuditEventId,
    bool RequiresOperatorConfirmation = true);

public sealed record LoginAssistConfirmRequest(
    Guid JobId,
    string AuditEventId,
    int StepIndex,
    bool Approved,
    /// <summary>Vault reference only — never a raw password.</summary>
    string? CredentialVaultRef,
    string? OperatorId);

public sealed record LoginAssistConfirmResponse(
    Guid JobId,
    string AuditEventId,
    int StepIndex,
    bool Approved,
    string Status,
    string Message);

public sealed record CrawlPageSnapshot(
    /// <summary>Opaque page id from Crawl.Api (stringified); AI does not allocate crawl pages.</summary>
    string PageId,
    string Url,
    int Depth,
    /// <summary>
    /// Precomputed Domain Link Ratio from .NET crawl domain — AI may cite but MUST NOT recompute.
    /// </summary>
    double? DomainLinkRatio,
    string? Status,
    string? Error);

public sealed record SummarizeRequest(
    Guid JobId,
    string SeedUrl,
    string Status,
    int PageCount,
    int EdgeCount,
    IReadOnlyList<CrawlPageSnapshot> Pages,
    IReadOnlyList<string>? Failures);

public sealed record SummarizeResponse(
    Guid JobId,
    string Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> CitedUrls,
    bool UsedModel,
    string? DegradedReason);

public sealed record AskTheCrawlRequest(
    Guid JobId,
    string Question,
    IReadOnlyList<CrawlPageSnapshot> Pages);

public sealed record AskTheCrawlCitation(string Url, string? PageId, string Snippet);

public sealed record AskTheCrawlResponse(
    Guid JobId,
    string Answer,
    IReadOnlyList<AskTheCrawlCitation> Citations,
    bool UsedModel,
    string? DegradedReason);

public sealed record AgentToolCall(string ToolName, IDictionary<string, string> Arguments, string Purpose);

public sealed record AgentPlanRequest(
    Guid JobId,
    string Goal,
    IReadOnlyList<CrawlPageSnapshot>? Pages,
    string? PageHtmlSnippet);

public sealed record AgentPlanResponse(
    Guid JobId,
    Guid PlanId,
    string RoleSignal,
    IReadOnlyList<string> PlanSteps,
    IReadOnlyList<AgentToolCall> ProposedToolCalls,
    bool RequiresHumanConfirm = true);

public sealed record AgentConfirmRequest(
    Guid PlanId,
    Guid JobId,
    bool Approved,
    string? OperatorId,
    IReadOnlyList<string>? ApprovedToolNames);

public sealed record AgentConfirmResponse(
    Guid PlanId,
    Guid JobId,
    string Status,
    IReadOnlyList<string> ExecutedTools,
    IReadOnlyList<string> Results,
    string RoleSignal);

public sealed record AiFeatureFlagsResponse(
    bool Enabled,
    bool FieldFinderEnabled,
    bool LoginAssistEnabled,
    bool SummarizationEnabled,
    bool AskTheCrawlEnabled,
    bool AgentLoopEnabled,
    bool DeepSeekConfigured);

public sealed record LoginAssistAuditEvent(
    string EventId,
    Guid JobId,
    string Kind,
    DateTimeOffset At,
    string? OperatorId,
    string Detail,
    /// <summary>Never contains passwords — vault refs only when present.</summary>
    string? CredentialVaultRef);
