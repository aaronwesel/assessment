using Microsoft.AspNetCore.SignalR;

namespace Crawl.Api.Realtime;

/// <summary>
/// Live crawl job progress hub. Clients call <see cref="SubscribeJob"/> then receive <c>jobProgress</c> events.
/// REST polling remains the PDF-safe fallback when the hub is unavailable.
/// </summary>
public sealed class CrawlJobsHub : Hub
{
    public static string GroupName(Guid jobId) => $"job:{jobId:D}";

    public Task SubscribeJob(Guid jobId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(jobId));

    public Task UnsubscribeJob(Guid jobId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(jobId));
}
