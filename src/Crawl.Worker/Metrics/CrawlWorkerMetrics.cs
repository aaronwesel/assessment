using System.Diagnostics.Metrics;

namespace Crawl.Worker.Metrics;

/// <summary>OpenTelemetry-friendly counters for crawl job progress and fetch health.</summary>
public static class CrawlWorkerMetrics
{
    public static readonly Meter Meter = new("Crawl.Worker", "1.0.0");

    public static readonly Counter<long> PagesProcessed = Meter.CreateCounter<long>(
        "crawl.pages.processed",
        description: "Pages that reached a terminal Done status");

    public static readonly Counter<long> PagesFailed = Meter.CreateCounter<long>(
        "crawl.pages.failed",
        description: "Pages marked Failed");

    public static readonly Counter<long> PagesSkipped = Meter.CreateCounter<long>(
        "crawl.pages.skipped",
        description: "Pages marked Skipped (non-HTML, robots, too large)");

    public static readonly Counter<long> PagesRobotsBlocked = Meter.CreateCounter<long>(
        "crawl.pages.robots_blocked",
        description: "Fetches blocked by robots.txt");

    public static readonly Counter<long> TransientRetries = Meter.CreateCounter<long>(
        "crawl.retries.transient",
        description: "Transient failures that triggered a requeue");

    public static readonly Counter<long> PoisonMessages = Meter.CreateCounter<long>(
        "crawl.messages.poison",
        description: "Messages routed to DLQ as poison / exhausted retries");

    public static readonly Counter<long> PagesClaimed = Meter.CreateCounter<long>(
        "crawl.pages.claimed",
        description: "Successful Queued→Processing claims");

    public static readonly Counter<long> PageReclaims = Meter.CreateCounter<long>(
        "crawl.pages.reclaimed",
        description: "Redelivered Processing pages reclaimed after worker crash");

    public static readonly Counter<long> DuplicateMessagesSkipped = Meter.CreateCounter<long>(
        "crawl.messages.duplicate_skipped",
        description: "At-least-once duplicates skipped (terminal or owned by another worker)");

    public static readonly Histogram<double> FetchDurationMs = Meter.CreateHistogram<double>(
        "crawl.fetch.duration_ms",
        unit: "ms",
        description: "HTML fetch duration including politeness wait ownership window");
}
