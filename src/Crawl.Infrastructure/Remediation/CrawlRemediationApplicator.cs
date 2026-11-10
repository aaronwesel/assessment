using Crawl.Domain.Incidents;
using Crawl.Infrastructure.Egress;
using Crawl.Infrastructure.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Remediation;

/// <summary>
/// Applies safe, configurable remediations against Scrapy-inspired engine knobs + egress pool.
/// </summary>
public sealed class CrawlRemediationApplicator(
    HostPolitenessGate politeness,
    IEgressProxyProvider egress,
    IOptions<CrawlHttpOptions> httpOptions,
    ILogger<CrawlRemediationApplicator> logger) : ICrawlRemediationApplicator
{
    public Task<RemediationApplyResult> ApplyAsync(
        CrawlFaultEvent fault,
        RemediationKind kind,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Apply(fault, kind));
    }

    private RemediationApplyResult Apply(CrawlFaultEvent fault, RemediationKind kind)
    {
        switch (kind)
        {
            case RemediationKind.None:
                return new RemediationApplyResult(false, "skipped", "No remediation for fault.");

            case RemediationKind.AlertOnly:
                return new RemediationApplyResult(true, "alert_only", "Remediation deferred to alert channel.");

            case RemediationKind.BackoffOnly:
            case RemediationKind.BackoffAndPolitenessBump:
            {
                var host = fault.Host ?? "unknown";
                var delay = BackoffPolicy.ComputeDelay(
                    fault.Attempt,
                    fault.RetryAfterSeconds,
                    baseDelay: TimeSpan.FromMilliseconds(Math.Max(250, httpOptions.Value.PolitenessDelayMilliseconds)));

                politeness.ScheduleBackoff(host, delay);
                double? multiplier = null;
                var detail = $"Scheduled backoff {delay.TotalSeconds:0.#}s for host={host}";

                if (kind == RemediationKind.BackoffAndPolitenessBump)
                {
                    multiplier = politeness.BumpPoliteness(host, BackoffPolicy.DefaultPolitenessBumpMultiplier);
                    detail += $"; politeness multiplier now {multiplier:0.##}x";
                }

                logger.LogInformation(
                    "Applied {Kind} for JobId={JobId} CorrelationId={CorrelationId}: {Detail}",
                    kind,
                    fault.JobId,
                    fault.CorrelationId,
                    detail);

                return new RemediationApplyResult(true, "applied", detail, delay, multiplier);
            }

            case RemediationKind.SwitchEgressProxy:
            {
                if (!egress.TrySwitchProxy(out var proxy, out var reason))
                {
                    logger.LogWarning(
                        "Proxy remediation fail-closed JobId={JobId}: {Reason}",
                        fault.JobId,
                        reason);
                    return new RemediationApplyResult(false, "fail_closed", reason);
                }

                var described = proxy is null
                    ? "unknown"
                    : $"{proxy.Scheme}://{proxy.Host}:{proxy.Port}";
                logger.LogInformation(
                    "Switched egress proxy for JobId={JobId} to {Proxy}",
                    fault.JobId,
                    described);
                return new RemediationApplyResult(true, "applied", reason, ProxyEndpoint: described);
            }

            default:
                return new RemediationApplyResult(false, "skipped", $"Unhandled remediation kind {kind}");
        }
    }
}
