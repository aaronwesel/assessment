namespace Crawl.Domain.Incidents;

/// <summary>
/// Safe Retry-After / exponential backoff for rate-limit remediations.
/// Mirrors Scrapy DOWNLOAD_DELAY politeness bumps — pure math, no I/O.
/// </summary>
public static class BackoffPolicy
{
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(5);
    public const double DefaultPolitenessBumpMultiplier = 2.0;
    public const double MaxPolitenessBumpMultiplier = 16.0;

    /// <summary>
    /// Compute wait before next fetch. Prefer Retry-After when present; otherwise exponential.
    /// </summary>
    public static TimeSpan ComputeDelay(
        int attempt,
        int? retryAfterSeconds = null,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        double jitterFactor = 0.2,
        Random? random = null)
    {
        var max = maxDelay ?? DefaultMaxDelay;
        if (max < TimeSpan.Zero)
            max = DefaultMaxDelay;

        if (retryAfterSeconds is int ra && ra > 0)
        {
            var fromHeader = TimeSpan.FromSeconds(Math.Min(ra, max.TotalSeconds));
            return Clamp(fromHeader, TimeSpan.Zero, max);
        }

        var basis = baseDelay ?? DefaultBaseDelay;
        if (basis < TimeSpan.Zero)
            basis = DefaultBaseDelay;

        var safeAttempt = Math.Clamp(attempt, 1, 16);
        var expMs = basis.TotalMilliseconds * Math.Pow(2, safeAttempt - 1);
        var capped = Math.Min(expMs, max.TotalSeconds * 1000);

        if (jitterFactor > 0)
        {
            var rng = random ?? Random.Shared;
            var jitter = 1.0 + ((rng.NextDouble() * 2) - 1) * Math.Clamp(jitterFactor, 0, 1);
            capped *= jitter;
        }

        return Clamp(TimeSpan.FromMilliseconds(capped), TimeSpan.Zero, max);
    }

    /// <summary>
    /// Next per-host politeness multiplier after a rate-limit / politeness bump remediation.
    /// </summary>
    public static double NextPolitenessMultiplier(
        double current,
        double bump = DefaultPolitenessBumpMultiplier,
        double max = MaxPolitenessBumpMultiplier)
    {
        var safeCurrent = current <= 0 ? 1.0 : current;
        var safeBump = bump < 1.0 ? DefaultPolitenessBumpMultiplier : bump;
        var safeMax = max < 1.0 ? MaxPolitenessBumpMultiplier : max;
        return Math.Min(safeCurrent * safeBump, safeMax);
    }

    /// <summary>
    /// Suggested remediation kind for a classified fault (deterministic default before AI overlay).
    /// </summary>
    public static RemediationKind SuggestRemediation(CrawlFaultCode code) =>
        code switch
        {
            CrawlFaultCode.RateLimited or CrawlFaultCode.ServiceUnavailable
                => RemediationKind.BackoffAndPolitenessBump,
            CrawlFaultCode.HardBlock or CrawlFaultCode.CaptchaOrBlock
                => RemediationKind.SwitchEgressProxy,
            CrawlFaultCode.Timeout or CrawlFaultCode.ConnectionReset or CrawlFaultCode.DnsFailure
                => RemediationKind.BackoffOnly,
            CrawlFaultCode.JobFailed or CrawlFaultCode.RepeatedDlq
                => RemediationKind.AlertOnly,
            _ => RemediationKind.None
        };

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

public enum RemediationKind
{
    None = 0,
    BackoffOnly = 1,
    BackoffAndPolitenessBump = 2,
    SwitchEgressProxy = 3,
    AlertOnly = 4
}
