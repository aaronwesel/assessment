using Crawl.Domain.Incidents;

namespace Crawl.Infrastructure.Remediation;

public interface ICrawlRemediationApplicator
{
    Task<RemediationApplyResult> ApplyAsync(
        CrawlFaultEvent fault,
        RemediationKind kind,
        CancellationToken cancellationToken = default);
}

public sealed record RemediationApplyResult(
    bool Applied,
    string Status,
    string Detail,
    TimeSpan? BackoffApplied = null,
    double? PolitenessMultiplier = null,
    string? ProxyEndpoint = null);
