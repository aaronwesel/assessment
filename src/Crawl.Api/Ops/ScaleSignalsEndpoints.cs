using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crawl.Api.Ops;

/// <summary>
/// Local scale signals: RabbitMQ depth + in-flight page counts for ASG/ECS hooks and demos.
/// </summary>
public static class ScaleSignalsEndpoints
{
    public static RouteGroupBuilder MapScaleSignals(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ops").WithTags("Ops");

        group.MapGet("/queue", (RabbitMqQueueStats stats) =>
            {
                var snap = stats.ReadPageQueue();
                return Results.Ok(new
                {
                    queue = snap.Queue,
                    messagesReady = snap.MessagesReady,
                    consumerCount = snap.ConsumerCount,
                    dlqMessages = snap.DlqMessages,
                    suggestScaleOut = snap.SuggestScaleOut(),
                    suggestScaleIn = snap.SuggestScaleIn(),
                    healthy = snap.Healthy,
                    error = snap.Error,
                    atUtc = snap.AtUtc,
                    note = "Primary scale path: add Crawl.Worker consumers on RabbitMQ (ECS desired_count / ASG). Per-job EC2 provisioner is stretch-next, not graded core."
                });
            })
            .WithName("GetQueueDepth")
            .WithOpenApi();

        group.MapGet("/inflight", async (CrawlDbContext db, CancellationToken ct) =>
            {
                var queued = await db.Pages.CountAsync(p => p.Status == Domain.Pages.CrawlPageStatus.Queued, ct);
                var processing = await db.Pages.CountAsync(p => p.Status == Domain.Pages.CrawlPageStatus.Processing, ct);
                var activeJobs = await db.Jobs.CountAsync(
                    j => j.Status == Domain.Jobs.CrawlJobStatus.Pending
                         || j.Status == Domain.Jobs.CrawlJobStatus.Running,
                    ct);

                return Results.Ok(new
                {
                    pagesQueued = queued,
                    pagesProcessing = processing,
                    activeJobs,
                    atUtc = DateTimeOffset.UtcNow
                });
            })
            .WithName("GetInflightPages")
            .WithOpenApi();

        return group;
    }
}
