using Crawl.Ai.Alerts;
using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Domain.Messaging;
using Crawl.Domain.Pages;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crawl.Api.Jobs;

/// <summary>
/// HTTP-facing crawl job orchestration: normalize seed URL, persist root page, publish first RabbitMQ message.
/// Reads are no-tracking; cancel is cooperative (worker checks status before fetch/enqueue children).
/// </summary>
public sealed class JobService(
    CrawlDbContext db,
    ICrawlMessagePublisher publisher,
    IJobSubscriberAlertService subscriberAlerts,
    ILogger<JobService> logger)
{
    public async Task<(CreateJobResponse? Response, string? Error)> CreateAsync(
        CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        if (!UrlNormalizer.TryNormalize(request.Url, out var normalized))
            return (null, "URL must be an absolute http(s) address.");

        var maxDepth = request.MaxDepth ?? CrawlJob.DefaultMaxDepth;
        var maxPages = request.MaxPages ?? CrawlJob.DefaultMaxPages;

        if (maxDepth is < 0 or > CrawlJob.AbsoluteMaxDepth)
            return (null, $"maxDepth must be between 0 and {CrawlJob.AbsoluteMaxDepth}.");

        if (maxPages is < 1 or > CrawlJob.DefaultMaxPages)
            return (null, $"maxPages must be between 1 and {CrawlJob.DefaultMaxPages}.");

        var now = DateTimeOffset.UtcNow;
        var job = CrawlJob.Create(normalized, maxDepth, maxPages, now);
        var root = CrawlPage.CreateRoot(job.Id, normalized, now);

        db.Jobs.Add(job);
        db.Pages.Add(root);
        await db.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            new CrawlPageRequested(job.Id, root.Id, root.Url, root.Depth, now),
            cancellationToken);

        logger.LogInformation("Created crawl job {JobId} for {Url}", job.Id, job.Url);
        return (new CreateJobResponse(job.Id), null);
    }

    public async Task<JobSummaryDto?> GetSummaryAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return null;

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .ToListAsync(cancellationToken);

        return JobDtoMapper.ToSummary(job, pages);
    }

    public async Task<JobTreeDto?> GetTreeAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return null;

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .ToListAsync(cancellationToken);

        return JobDtoMapper.ToTree(job, pages);
    }

    public async Task<IReadOnlyList<PageRowDto>?> GetPagesAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var exists = await db.Jobs.AsNoTracking().AnyAsync(j => j.Id == jobId, cancellationToken);
        if (!exists)
            return null;

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .ToListAsync(cancellationToken);

        return JobDtoMapper.ToFlatPages(pages);
    }

    public async Task<IReadOnlyList<PageLinkDto>?> GetPageLinksAsync(
        Guid jobId,
        long pageId,
        CancellationToken cancellationToken)
    {
        var pageExists = await db.Pages.AsNoTracking()
            .AnyAsync(p => p.JobId == jobId && p.Id == pageId, cancellationToken);
        if (!pageExists)
            return null;

        return await db.Edges.AsNoTracking()
            .Where(e => e.SourcePageId == pageId)
            .OrderBy(e => e.TargetUrl)
            .Select(e => new PageLinkDto(e.TargetUrl, e.IsInternal))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedHistoryDto> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Jobs.AsNoTracking().OrderByDescending(j => j.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var jobs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = jobs.Select(j => new JobHistoryItemDto(
            j.Id,
            j.Url,
            j.Status.ToString(),
            j.CreatedAt,
            j.CompletedAt)).ToList();

        return new PagedHistoryDto(items, page, pageSize, total);
    }

    public async Task<(bool Found, bool Canceled, string? Error)> CancelAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return (false, false, null);

        if (job.Status.IsTerminal())
            return (true, false, $"Job already {job.Status}.");

        job.MarkCanceled(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await subscriberAlerts.TrySendTerminalStatusAlertAsync(job, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Canceled crawl job {JobId}", jobId);
        return (true, true, null);
    }

    public async Task<(bool Found, JobSummaryDto? Summary, string? Error)> SetAlertEmailAsync(
        Guid jobId,
        SetJobAlertEmailRequest request,
        CancellationToken cancellationToken)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
            return (false, null, null);

        try
        {
            job.SetAlertEmail(request.Email);
        }
        catch (ArgumentException ex)
        {
            return (true, null, ex.Message);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (job.Status.IsTerminal() && job.HasAlertSubscription && !job.StatusAlertSent)
        {
            await subscriberAlerts.TrySendTerminalStatusAlertAsync(job, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .ToListAsync(cancellationToken);
        return (true, JobDtoMapper.ToSummary(job, pages), null);
    }
}
