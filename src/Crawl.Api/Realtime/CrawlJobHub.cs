using Microsoft.AspNetCore.SignalR;

namespace Crawl.Api.Realtime;

/// <summary>
/// Job Details live progress hub. Clients join <c>job:{jobId}</c>; REST remains source of truth if WS drops.
/// </summary>
public sealed class CrawlJobHub : Hub
{
    public static string GroupName(Guid jobId) => $"job:{jobId:D}";

    public Task SubscribeJob(Guid jobId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(jobId));

    public Task UnsubscribeJob(Guid jobId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(jobId));
}
