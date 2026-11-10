using Crawl.Ai.Incidents;
using Crawl.Domain.Fetching;
using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Domain.Messaging;
using Crawl.Domain.Pages;
using Crawl.Domain.Scheduling;
using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Worker.Fetching;
using Crawl.Worker.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Crawl.Worker.Processing;

public sealed class CrawlPageProcessor(
    CrawlDbContext db,
    HtmlPageFetcher fetcher,
    HtmlLinkExtractor linkExtractor,
    ICrawlMessagePublisher publisher,
    ICrawlProgressPublisher progressPublisher,
    ICrawlIncidentMonitor incidentMonitor,
    IOptions<CrawlHttpOptions> httpOptions,
    ILogger<CrawlPageProcessor> logger)
{
    public async Task ProcessAsync(
        CrawlPageRequested message,
        bool redelivered,
        CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = message.JobId,
            ["PageId"] = message.PageId,
            ["Depth"] = message.Depth,
            ["Redelivered"] = redelivered
        });

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Dropping message for missing job {JobId}", message.JobId);
            return;
        }

        if (job.Status is CrawlJobStatus.Canceled)
        {
            logger.LogInformation("Skipping page {PageId}; job canceled", message.PageId);
            await PublishProgressAsync(job, cancellationToken);
            return;
        }

        var claim = await TryClaimPageAsync(message.PageId, message.JobId, redelivered, cancellationToken);
        if (claim != PageClaimResult.Claimed)
        {
            logger.LogInformation(
                "Page {PageId} claim={Claim}; acknowledging without double-work",
                message.PageId,
                claim);
            if (claim is PageClaimResult.AlreadyTerminal or PageClaimResult.OwnedByOther)
                await PublishProgressAsync(job, cancellationToken);
            return;
        }

        var page = await db.Pages.FirstAsync(
            p => p.Id == message.PageId && p.JobId == message.JobId,
            cancellationToken);

        job.MarkRunning(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await PublishProgressAsync(job, cancellationToken);

        var normalizeOptions = new UrlNormalizationOptions
        {
            SortQueryParameters = httpOptions.Value.SortQueryParameters,
            MaxUrlLength = CrawlJob.UrlMaxLength
        };

        if (!UrlNormalizer.TryNormalize(page.Url, baseUri: null, normalizeOptions, out var pageUri))
        {
            page.MarkFailed("Invalid page URL.", DateTimeOffset.UtcNow);
            CrawlWorkerMetrics.PagesFailed.Add(1);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        FetchResult fetch;
        try
        {
            fetch = await fetcher.FetchAsync(pageUri, cancellationToken);
        }
        catch (TransientCrawlException)
        {
            // Reset to Queued so a retry/redelivery can reclaim after transient failure.
            await ResetPageToQueuedAsync(page.Id, cancellationToken);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            await ResetPageToQueuedAsync(page.Id, cancellationToken);
            throw new TransientCrawlException($"Transient error fetching {pageUri}", ex);
        }

        var now = DateTimeOffset.UtcNow;
        if (fetch.Skipped)
        {
            page.MarkSkipped(fetch.Error ?? "Skipped", now);
            CrawlWorkerMetrics.PagesSkipped.Add(1);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        if (!fetch.Success || fetch.Html is null)
        {
            page.MarkFailed(fetch.Error ?? "Fetch failed", now);
            CrawlWorkerMetrics.PagesFailed.Add(1);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        var parseBase = fetch.FinalUrl ?? pageUri;
        var links = linkExtractor.Extract(fetch.Html, parseBase, normalizeOptions);
        var startHost = job.StartHost;
        var internalCount = 0;
        var edges = new List<PageEdge>();
        var childCandidates = new List<Uri>();

        foreach (var link in links)
        {
            var isInternal = string.Equals(link.IdnHost, startHost, StringComparison.OrdinalIgnoreCase);
            if (isInternal)
            {
                internalCount++;
                childCandidates.Add(link);
            }

            edges.Add(PageEdge.Create(page.Id, link, isInternal));
        }

        // Replace edges idempotently: delete existing then insert (unique index guards races).
        var existingEdges = await db.Edges.Where(e => e.SourcePageId == page.Id).ToListAsync(cancellationToken);
        db.Edges.RemoveRange(existingEdges);
        db.Edges.AddRange(edges);

        page.MarkDone(links.Count, internalCount, now);
        CrawlWorkerMetrics.PagesProcessed.Add(1);
        await db.SaveChangesAsync(cancellationToken);

        var childMessages = new List<CrawlPageRequested>();
        if (job.CanEnqueueChildrenOf(page.Depth) && job.Status != CrawlJobStatus.Canceled)
        {
            // Stable BFS fan-out: lexicographic URL order within the next depth.
            childCandidates = childCandidates
                .OrderBy(u => u.AbsoluteUri, StringComparer.Ordinal)
                .ToList();
            childMessages = await EnqueueChildrenAsync(job, page, childCandidates, now, cancellationToken);
        }

        await ReconcileJobAsync(job, cancellationToken);

        foreach (var childMessage in childMessages)
            await publisher.PublishAsync(childMessage, cancellationToken);

        logger.LogInformation(
            "Processed page {PageId} links={LinkCount} internal={InternalCount} ratio={Ratio} children={Children}",
            page.Id,
            page.LinkCount,
            page.InternalLinkCount,
            page.DomainLinkRatioValue,
            childMessages.Count);
    }

    /// <summary>
    /// Atomic multi-worker claim: Queued→Processing wins; redelivered Processing may reclaim after crash;
    /// non-redelivered Processing means another worker owns it (ack duplicate).
    /// </summary>
    internal static PageClaimResult EvaluateClaim(
        CrawlPageStatus? status,
        bool redelivered,
        int queuedClaimRows)
    {
        if (status is null)
            return PageClaimResult.Missing;

        if (status.Value.IsTerminal())
            return PageClaimResult.AlreadyTerminal;

        if (queuedClaimRows > 0)
            return PageClaimResult.Claimed;

        if (status == CrawlPageStatus.Processing && redelivered)
            return PageClaimResult.Claimed;

        if (status == CrawlPageStatus.Processing)
            return PageClaimResult.OwnedByOther;

        // Queued but update lost the race — treat as owned by other.
        if (status == CrawlPageStatus.Queued)
            return PageClaimResult.OwnedByOther;

        return PageClaimResult.AlreadyTerminal;
    }

    private async Task<PageClaimResult> TryClaimPageAsync(
        long pageId,
        Guid jobId,
        bool redelivered,
        CancellationToken cancellationToken)
    {
        var page = await db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pageId && p.JobId == jobId, cancellationToken);
        if (page is null)
            return PageClaimResult.Missing;

        if (page.Status.IsTerminal())
        {
            CrawlWorkerMetrics.DuplicateMessagesSkipped.Add(1);
            return PageClaimResult.AlreadyTerminal;
        }

        // Prefer exclusive Queued → Processing transition (prevents double-count).
        var claimed = await db.Pages
            .Where(p => p.Id == pageId && p.JobId == jobId && p.Status == CrawlPageStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.Status, CrawlPageStatus.Processing),
                cancellationToken);

        var result = EvaluateClaim(page.Status, redelivered, claimed);
        if (result == PageClaimResult.Claimed && claimed == 0 && redelivered)
        {
            // Crash mid-page: page already Processing; re-attach tracked entity.
            CrawlWorkerMetrics.PageReclaims.Add(1);
        }
        else if (result == PageClaimResult.OwnedByOther)
        {
            CrawlWorkerMetrics.DuplicateMessagesSkipped.Add(1);
        }
        else if (result == PageClaimResult.Claimed && claimed > 0)
        {
            CrawlWorkerMetrics.PagesClaimed.Add(1);
        }

        if (result == PageClaimResult.Claimed)
        {
            // Ensure change tracker sees Processing for subsequent MarkDone/Failed.
            var tracked = await db.Pages.FirstAsync(p => p.Id == pageId && p.JobId == jobId, cancellationToken);
            if (tracked.Status != CrawlPageStatus.Processing)
                tracked.MarkProcessing();
        }

        return result;
    }

    private async Task ResetPageToQueuedAsync(long pageId, CancellationToken cancellationToken)
    {
        await db.Pages
            .Where(p => p.Id == pageId && p.Status == CrawlPageStatus.Processing)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(p => p.Status, CrawlPageStatus.Queued),
                cancellationToken);

        // Detach any tracked Processing entity so retry sees fresh state.
        foreach (var entry in db.ChangeTracker.Entries<CrawlPage>().Where(e => e.Entity.Id == pageId))
            entry.State = EntityState.Detached;
    }

    private async Task FinalizeAsync(CrawlJob job, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        await ReconcileJobAsync(job, cancellationToken);
    }

    private async Task<List<CrawlPageRequested>> EnqueueChildrenAsync(
        CrawlJob job,
        CrawlPage parent,
        IReadOnlyList<Uri> childCandidates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingUrls = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == job.Id)
            .Select(p => p.Url)
            .ToListAsync(cancellationToken);
        var known = existingUrls.ToHashSet(StringComparer.Ordinal);
        var remaining = job.RemainingPageSlots(known.Count);
        var inserted = new List<CrawlPage>();

        foreach (var childUrl in childCandidates)
        {
            if (remaining <= 0)
                break;
            if (!known.Add(childUrl.AbsoluteUri))
                continue;

            var child = CrawlPage.CreateChild(job.Id, parent.Id, parent.Depth + 1, childUrl, now);
            db.Pages.Add(child);
            inserted.Add(child);
            remaining--;
        }

        if (inserted.Count == 0)
            return [];

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogDebug(ex, "Race inserting children for job {JobId}; reloading known URLs", job.Id);
            foreach (var entry in db.ChangeTracker.Entries<CrawlPage>().Where(e => e.State == EntityState.Added))
                entry.State = EntityState.Detached;

            // Best-effort: only publish pages that actually exist and are still queued under this parent.
            // Dedup MessageId ({jobId}:{pageId}) + claim logic makes duplicate publishes safe.
            var queuedChildren = await db.Pages.AsNoTracking()
                .Where(p => p.JobId == job.Id && p.ParentPageId == parent.Id && p.Status == CrawlPageStatus.Queued)
                .OrderBy(p => p.Url)
                .ToListAsync(cancellationToken);

            return queuedChildren
                .Select(c => new CrawlPageRequested(job.Id, c.Id, c.Url, c.Depth, now))
                .ToList();
        }

        return inserted
            .Select(c => new CrawlPageRequested(job.Id, c.Id, c.Url, c.Depth, now))
            .ToList();
    }

    private async Task ReconcileJobAsync(CrawlJob job, CancellationToken cancellationToken)
    {
        if (job.Status.IsTerminal())
        {
            await db.SaveChangesAsync(cancellationToken);
            await PublishProgressAsync(job, cancellationToken);
            return;
        }

        // Re-check cancel that may have landed while processing.
        await db.Entry(job).ReloadAsync(cancellationToken);
        if (job.Status.IsTerminal())
        {
            await PublishProgressAsync(job, cancellationToken);
            return;
        }

        var root = await db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.JobId == job.Id && p.Depth == 0 && p.ParentPageId == null, cancellationToken);

        var pending = await db.Pages.CountAsync(
            p => p.JobId == job.Id && (p.Status == CrawlPageStatus.Queued || p.Status == CrawlPageStatus.Processing),
            cancellationToken);

        var prior = job.Status;
        job.ReconcileAfterPageFinished(root?.Status ?? CrawlPageStatus.Failed, pending, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await PublishProgressAsync(job, cancellationToken);

        if (prior != CrawlJobStatus.Failed && job.Status == CrawlJobStatus.Failed)
        {
            try
            {
                var fault = new CrawlFaultEvent(
                    job.Id,
                    $"{job.Id:N}:job-failed",
                    CrawlFaultCode.JobFailed,
                    CrawlFaultSeverity.Error,
                    CrawlFaultClassifier.SanitizeSummary(job.FailureReason ?? "Job failed"),
                    Host: job.StartHost);
                await incidentMonitor.HandleAsync(fault, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to emit JobFailed incident for {JobId}", job.Id);
            }
        }
    }

    private async Task PublishProgressAsync(CrawlJob job, CancellationToken cancellationToken)
    {
        try
        {
            var pages = await db.Pages.AsNoTracking()
                .Where(p => p.JobId == job.Id)
                .Select(p => p.Status)
                .ToListAsync(cancellationToken);

            var queued = pages.Count(s => s == CrawlPageStatus.Queued);
            var processing = pages.Count(s => s == CrawlPageStatus.Processing);
            var done = pages.Count(s => s == CrawlPageStatus.Done);
            var skipped = pages.Count(s => s == CrawlPageStatus.Skipped);
            var failed = pages.Count(s => s == CrawlPageStatus.Failed);
            var terminal = done + skipped + failed;
            var denominator = Math.Max(pages.Count, 1);
            var progress = job.Status.IsTerminal()
                ? 100
                : (int)Math.Clamp(Math.Round(100.0 * terminal / denominator), 0, 99);

            var pageEntities = await db.Pages.AsNoTracking()
                .Where(p => p.JobId == job.Id)
                .ToListAsync(cancellationToken);
            await progressPublisher.PublishAsync(
                CrawlJobProgressEvent.FromJobAndPages(
                    job,
                    pageEntities,
                    CrawlJobProgressEvent.KindPageProgress),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Progress publish failed for {JobId} (SignalR clients may poll)", job.Id);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
