namespace Crawl.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string PageQueue { get; set; } = "crawl.page";
    public string PageDlq { get; set; } = "crawl.page.dlq";
    /// <summary>Fanout exchange for live job progress (API SignalR relay).</summary>
    public string JobEventsExchange { get; set; } = "crawl.job.events";
    public int PrefetchCount { get; set; } = 8;
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Declare crawl.page with x-max-priority and publish depth-based priorities (BFS-aware).
    /// If an older queue exists without this arg, delete it once or set false.
    /// </summary>
    public bool EnableDepthPriority { get; set; } = true;

    public byte MaxDepthPriority { get; set; } = 10;
}
