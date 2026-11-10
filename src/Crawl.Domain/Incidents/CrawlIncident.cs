namespace Crawl.Domain.Incidents;

/// <summary>
/// Persisted crawl incident + last remediation action (PHI-safe fields only).
/// </summary>
public class CrawlIncident
{
    public const int SummaryMaxLength = 500;
    public const int DetailMaxLength = 1000;

    private CrawlIncident() { }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public CrawlFaultCode FaultCode { get; private set; }
    public CrawlFaultSeverity Severity { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string? Host { get; private set; }
    public long? PageId { get; private set; }
    public int? HttpStatus { get; private set; }
    public RemediationKind RemediationKind { get; private set; }
    public string RemediationStatus { get; private set; } = "pending";
    public string? RemediationDetail { get; private set; }
    public bool UsedModel { get; private set; }
    public string? AiAdvice { get; private set; }
    public bool AlertSent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static CrawlIncident Create(CrawlFaultEvent fault, RemediationKind kind)
    {
        return new CrawlIncident
        {
            Id = Guid.NewGuid(),
            JobId = fault.JobId,
            CorrelationId = string.IsNullOrWhiteSpace(fault.CorrelationId)
                ? Guid.NewGuid().ToString("N")
                : fault.CorrelationId.Trim(),
            FaultCode = fault.Code,
            Severity = fault.Severity,
            Summary = Truncate(CrawlFaultClassifier.SanitizeSummary(fault.Summary), SummaryMaxLength) ?? "crawl fault",
            Host = Truncate(fault.Host, 255),
            PageId = fault.PageId,
            HttpStatus = fault.HttpStatus,
            RemediationKind = kind,
            RemediationStatus = "pending",
            CreatedAt = fault.At
        };
    }

    public void ApplyRemediation(
        string status,
        string? detail,
        bool usedModel,
        string? aiAdvice,
        bool alertSent)
    {
        RemediationStatus = Truncate(status, 64) ?? "unknown";
        RemediationDetail = Truncate(detail, DetailMaxLength);
        UsedModel = usedModel;
        AiAdvice = Truncate(aiAdvice, DetailMaxLength);
        AlertSent = alertSent;
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return null;
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
