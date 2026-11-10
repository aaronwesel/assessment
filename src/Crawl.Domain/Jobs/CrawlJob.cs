using Crawl.Domain.Links;
using Crawl.Domain.Pages;

namespace Crawl.Domain.Jobs;

/// <summary>
/// Crawl job aggregate: seed URL, depth/page caps, lifecycle. <see cref="StartHost"/> anchors Domain Link Ratio.
/// </summary>
public class CrawlJob
{
    public const int UrlMaxLength = 2048;
    public const int FailureReasonMaxLength = 500;
    public const int AlertEmailMaxLength = 320;
    public const int DefaultMaxDepth = 2;
    public const int DefaultMaxPages = 5000;
    /// <summary>Per-job hard cap for maxPages (aligned with free billing page allowance).</summary>
    public const int AbsoluteMaxPages = 5000;
    public const int AbsoluteMaxDepth = 5;

    private CrawlJob() { }

    public Guid Id { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string StartHost { get; private set; } = string.Empty;
    public int MaxDepth { get; private set; }
    public int MaxPages { get; private set; }
    public CrawlJobStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>Operator email for job completion / issue alerts (guided step 5). PHI-free templates only.</summary>
    public string? AlertEmail { get; private set; }

    /// <summary>True after a terminal-status subscriber alert was attempted for this job.</summary>
    public bool StatusAlertSent { get; private set; }

    public ICollection<CrawlPage> Pages { get; private set; } = new List<CrawlPage>();

    public bool HasAlertSubscription => !string.IsNullOrWhiteSpace(AlertEmail);

    public bool CanEnqueueChildrenOf(int parentDepth) => parentDepth < MaxDepth;

    public int RemainingPageSlots(int existingPageCount) => Math.Max(0, MaxPages - existingPageCount);

    public void MarkRunning(DateTimeOffset nowUtc)
    {
        if (Status.IsTerminal())
            return;

        if (Status == CrawlJobStatus.Pending)
        {
            Status = CrawlJobStatus.Running;
            StartedAt ??= nowUtc;
        }
    }

    public void MarkCanceled(DateTimeOffset nowUtc)
    {
        if (Status.IsTerminal())
            return;

        Status = CrawlJobStatus.Canceled;
        StartedAt ??= nowUtc;
        CompletedAt = nowUtc;
    }

    public void MarkFailed(string reason, DateTimeOffset nowUtc)
    {
        if (Status.IsTerminal())
            return;

        Status = CrawlJobStatus.Failed;
        StartedAt ??= nowUtc;
        CompletedAt = nowUtc;
        FailureReason = Truncate(reason, FailureReasonMaxLength);
    }

    /// <summary>
    /// After each page terminal transition: fail job if seed unrunnable; else complete when no Queued/Processing pages remain.
    /// </summary>
    public void ReconcileAfterPageFinished(CrawlPageStatus rootPageStatus, int queuedOrProcessingCount, DateTimeOffset nowUtc)
    {
        if (Status.IsTerminal())
            return;

        StartedAt ??= nowUtc;

        if (rootPageStatus is CrawlPageStatus.Failed or CrawlPageStatus.Skipped)
        {
            MarkFailed("Start page could not be crawled.", nowUtc);
            return;
        }

        if (queuedOrProcessingCount == 0)
        {
            Status = CrawlJobStatus.Completed;
            CompletedAt = nowUtc;
            return;
        }

        Status = CrawlJobStatus.Running;
    }

    public void SetAlertEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            AlertEmail = null;
            return;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > AlertEmailMaxLength)
            throw new ArgumentException($"Email must be at most {AlertEmailMaxLength} characters.", nameof(email));

        if (!LooksLikeEmail(trimmed))
            throw new ArgumentException("Email address format is invalid.", nameof(email));

        AlertEmail = trimmed;
    }

    /// <returns>False when a status alert was already marked sent.</returns>
    public bool TryMarkStatusAlertSent()
    {
        if (StatusAlertSent)
            return false;

        StatusAlertSent = true;
        return true;
    }

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && !value.Contains(' ');
    }

    public static CrawlJob Create(Uri normalizedStartUrl, int maxDepth, int maxPages, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(normalizedStartUrl);
        if (!UrlNormalizer.TryNormalize(normalizedStartUrl.AbsoluteUri, out var normalized))
            throw new ArgumentException("Start URL must be absolute http(s).", nameof(normalizedStartUrl));

        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxDepth, AbsoluteMaxDepth);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPages, AbsoluteMaxPages);

        return new CrawlJob
        {
            Id = Guid.NewGuid(),
            Url = normalized.AbsoluteUri,
            StartHost = normalized.IdnHost.ToLowerInvariant(),
            MaxDepth = maxDepth,
            MaxPages = maxPages,
            Status = CrawlJobStatus.Pending,
            CreatedAt = createdAtUtc
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
