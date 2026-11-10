using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Domain.Messaging;
using Crawl.Domain.Pages;
using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Worker.Fetching;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Crawl.Worker.Processing;

public sealed class CrawlPageProcessor(
    CrawlDbContext db,
    HtmlPageFetcher fetcher,
    HtmlLinkExtractor linkExtractor,
    ICrawlMessagePublisher publisher,
    ILogger<CrawlPageProcessor> logger)
{
    public async Task ProcessAsync(CrawlPageRequested message, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = message.JobId,
            ["PageId"] = message.PageId
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
            return;
        }

        var page = await db.Pages.FirstOrDefaultAsync(
            p => p.Id == message.PageId && p.JobId == message.JobId,
            cancellationToken);

        if (page is null)
        {
            logger.LogWarning("Dropping message for missing page {PageId}", message.PageId);
            return;
        }

        // Idempotency: already terminal pages are no-ops on redelivery.
        if (page.Status.IsTerminal())
        {
            logger.LogInformation("Page {PageId} already {Status}; acknowledging redelivery", page.Id, page.Status);
            await ReconcileJobAsync(job, cancellationToken);
            return;
        }

        job.MarkRunning(DateTimeOffset.UtcNow);
        page.MarkProcessing();
        await db.SaveChangesAsync(cancellationToken);

        if (!UrlNormalizer.TryNormalize(page.Url, out var pageUri))
        {
            page.MarkFailed("Invalid page URL.", DateTimeOffset.UtcNow);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        FetchResult fetch;
        try
        {
            fetch = await fetcher.FetchAsync(pageUri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Let the consumer retry / DLQ for transient HTTP failures.
            throw;
        }

        var now = DateTimeOffset.UtcNow;
        if (fetch.Skipped)
        {
            page.MarkSkipped(fetch.Error ?? "Skipped", now);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        if (!fetch.Success || fetch.Html is null)
        {
            page.MarkFailed(fetch.Error ?? "Fetch failed", now);
            await FinalizeAsync(job, cancellationToken);
            return;
        }

        var links = linkExtractor.Extract(fetch.Html, pageUri);
        var startHost = job.StartHost;
        var internalCount = 0;
        var edges = new List<PageEdge>();
        var childCandidates = new List<Uri>();

        foreach (var link in links)
        {
            var isInternal = string.Equals(link.Host, startHost, StringComparison.OrdinalIgnoreCase);
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
        await db.SaveChangesAsync(cancellationToken);

        var childMessages = new List<CrawlPageRequested>();
        if (job.CanEnqueueChildrenOf(page.Depth) && job.Status != CrawlJobStatus.Canceled)
        {
            childMessages = await EnqueueChildrenAsync(job, page, childCandidates, now, cancellationToken);
        }

        await ReconcileJobAsync(job, cancellationToken);

        foreach (var childMessage in childMessages)
            await publisher.PublishAsync(childMessage, cancellationToken);

        logger.LogInformation(
            "Processed page {PageId} links={LinkCount} internal={InternalCount} children={Children}",
            page.Id,
            page.LinkCount,
            page.InternalLinkCount,
            childMessages.Count);
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
            var queuedChildren = await db.Pages.AsNoTracking()
                .Where(p => p.JobId == job.Id && p.ParentPageId == parent.Id && p.Status == CrawlPageStatus.Queued)
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
            return;
        }

        // Re-check cancel that may have landed while processing.
        await db.Entry(job).ReloadAsync(cancellationToken);
        if (job.Status.IsTerminal())
            return;

        var root = await db.Pages.AsNoTracking()
            .FirstOrDefaultAsync(p => p.JobId == job.Id && p.Depth == 0 && p.ParentPageId == null, cancellationToken);

        var pending = await db.Pages.CountAsync(
            p => p.JobId == job.Id && (p.Status == CrawlPageStatus.Queued || p.Status == CrawlPageStatus.Processing),
            cancellationToken);

        job.ReconcileAfterPageFinished(root?.Status ?? CrawlPageStatus.Failed, pending, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
