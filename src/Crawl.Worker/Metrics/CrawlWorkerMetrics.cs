using System.Diagnostics.Metrics;

namespace Crawl.Worker.Metrics;

/// <summary>OpenTelemetry-friendly counters for crawl job progress and fetch health.</summary>
public static class CrawlWorkerMetrics
{
    public static readonly Meter Meter = new("Crawl.Worker", "1.0.0");

    public static readonly Counter<long> PagesProcessed = Meter.CreateCounter<long>(
        "crawl.pages.processed",
        description: "Pages that reached a terminal Done status");

    /// <summary>Billable unit: parsed pages with link results (Done, not failed/skipped).</summary>
    public static readonly Counter<long> PagesWithResults = Meter.CreateCounter<long>(
        "crawl.pages.with_results",
        description: "Pages with parsed link results — Stripe meter unit in test mode");

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

    public static readonly Histogram<double> FetchDurationMs = Meter.CreateHistogram<double>(
        "crawl.fetch.duration_ms",
        unit: "ms",
        description: "HTML fetch duration including politeness wait ownership window");
}
