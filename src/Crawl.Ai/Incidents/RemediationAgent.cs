using System.Text.Json;
using Crawl.Ai.DeepSeek;
using Crawl.Domain.Incidents;
using Microsoft.Extensions.Logging;

namespace Crawl.Ai.Incidents;

public interface IRemediationAgent
{
    Task<RemediationAdvice> AdviseAsync(CrawlFaultEvent fault, CancellationToken cancellationToken = default);
}

public sealed record RemediationAdvice(
    RemediationKind Kind,
    string Rationale,
    bool UsedModel,
    string? DegradedReason);

/// <summary>
/// DeepSeek-assisted remediation chooser. Falls back to deterministic <see cref="BackoffPolicy.SuggestRemediation"/>.
/// Prompts include jobId/correlationId + fault summary only — no secrets/PHI/HTML bodies.
/// </summary>
public sealed class RemediationAgent(
    IDeepSeekClient deepSeek,
    ILogger<RemediationAgent> logger) : IRemediationAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<RemediationAdvice> AdviseAsync(
        CrawlFaultEvent fault,
        CancellationToken cancellationToken = default)
    {
        var fallback = BackoffPolicy.SuggestRemediation(fault.Code);
        var fallbackRationale = $"Deterministic policy for {fault.Code} → {fallback}";

        if (!deepSeek.IsConfigured)
        {
            return new RemediationAdvice(fallback, fallbackRationale, false, AiDegradedCopy.OfflinePolicy);
        }

        var system =
            "You are a crawl ops remediation assistant for an RCM portal crawler. " +
            "Choose ONE safe remediation. Never request secrets, passwords, cookies, or HTML bodies. " +
            "Never suggest parsing links or changing Domain Link Ratio. " +
            "Reply JSON only: {\"kind\":\"BackoffAndPolitenessBump|SwitchEgressProxy|BackoffOnly|AlertOnly|None\",\"rationale\":\"short\"}.";

        var user =
            $"jobId={fault.JobId:N}\n" +
            $"correlationId={fault.CorrelationId}\n" +
            $"faultCode={fault.Code}\n" +
            $"severity={fault.Severity}\n" +
            $"httpStatus={fault.HttpStatus?.ToString() ?? "n/a"}\n" +
            $"retryAfterSeconds={fault.RetryAfterSeconds?.ToString() ?? "n/a"}\n" +
            $"attempt={fault.Attempt}\n" +
            $"host={fault.Host ?? "n/a"}\n" +
            $"summary={CrawlFaultClassifier.SanitizeSummary(fault.Summary)}\n" +
            "Allowed kinds: BackoffAndPolitenessBump (rate limit), SwitchEgressProxy (IP/hard block; fail closed if no proxies), BackoffOnly, AlertOnly, None.";

        try
        {
            var result = await deepSeek.CompleteAsync(system, user, cancellationToken).ConfigureAwait(false);
            if (!result.UsedModel || string.IsNullOrWhiteSpace(result.Content))
            {
                return new RemediationAdvice(
                    fallback,
                    fallbackRationale,
                    false,
                    result.DegradedReason ?? "Model empty; using policy.");
            }

            var parsed = TryParse(result.Content, fallback);
            return new RemediationAdvice(parsed.Kind, parsed.Rationale, true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Remediation agent failed; using deterministic policy");
            return new RemediationAdvice(fallback, fallbackRationale, false, "Model error; using policy.");
        }
    }

    private static (RemediationKind Kind, string Rationale) TryParse(string content, RemediationKind fallback)
    {
        var json = content.Trim();
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start >= 0 && end > start)
            json = json[start..(end + 1)];

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var kindRaw = root.TryGetProperty("kind", out var k) ? k.GetString() : null;
            var rationale = root.TryGetProperty("rationale", out var r)
                ? CrawlFaultClassifier.SanitizeSummary(r.GetString() ?? fallback.ToString())
                : fallback.ToString();

            if (Enum.TryParse<RemediationKind>(kindRaw, ignoreCase: true, out var kind)
                && Enum.IsDefined(kind))
            {
                return (kind, rationale);
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return (fallback, "Unparseable model JSON; using deterministic policy.");
    }
}
