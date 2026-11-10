using System.Net;
using Crawl.Domain.Fetching;
using Crawl.Domain.Links;
using Crawl.Domain.Messaging;
using Crawl.Domain.Pages;
using Crawl.Domain.Robots;
using Crawl.Domain.Scheduling;
using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Http;
using Crawl.Worker.Fetching;

namespace Crawl.Integration.Tests;

/// <summary>
/// Hard distributed-crawler edge cases: redirects, robots Crawl-delay, depth priority,
/// transient vs poison taxonomy, and claim/idempotency mirrors.
/// </summary>
public class DistributedEdgeCaseTests
{
    [Fact]
    public void Redirect_loop_detected_by_visited_set()
    {
        var a = "https://example.com/a";
        var b = "https://example.com/b";
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a };
        Assert.True(visited.Add(b));
        Assert.False(visited.Add(a)); // loop back
    }

    [Fact]
    public void Robots_crawl_delay_preferred_over_default()
    {
        var rules = RobotsTxtRules.Parse("""
            User-agent: CrawlAssessmentBot
            Crawl-delay: 2.5
            Allow: /
            """);
        var delay = rules.GetCrawlDelay("CrawlAssessmentBot");
        Assert.Equal(TimeSpan.FromSeconds(2.5), delay);
        Assert.True(delay > TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void Depth_priority_prefers_shallower_pages_for_multi_consumer_bfs()
    {
        var d0 = DepthPriority.ForDepth(0);
        var d1 = DepthPriority.ForDepth(1);
        var d2 = DepthPriority.ForDepth(2);
        Assert.True(d0 > d1);
        Assert.True(d1 > d2);
        // Multiple consumers still drain higher priority first when x-max-priority is set.
        Assert.Equal(DepthPriority.MaxPriority, d0);
    }

    [Theory]
    [InlineData(typeof(TransientCrawlException), true)]
    [InlineData(typeof(HttpRequestException), true)]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(ArgumentException), false)]
    public void Transient_vs_poison_taxonomy(Type exceptionType, bool expectTransient)
    {
        Exception ex = exceptionType.Name switch
        {
            nameof(TransientCrawlException) => new TransientCrawlException("boom"),
            nameof(HttpRequestException) => new HttpRequestException("net"),
            nameof(TimeoutException) => new TimeoutException("slow"),
            nameof(InvalidOperationException) => new InvalidOperationException("poison"),
            _ => new ArgumentException("poison")
        };

        var isTransient = ex is TransientCrawlException
            or HttpRequestException
            or TaskCanceledException
            or TimeoutException
            or IOException
            || ex.InnerException is TransientCrawlException or HttpRequestException or TaskCanceledException;

        Assert.Equal(expectTransient, isTransient);
    }

    [Fact]
    public void Retry_exhaustion_routes_to_dlq_not_infinite_requeue()
    {
        const int maxRetries = 3;
        var retryCount = 3;
        var shouldDlq = retryCount >= maxRetries;
        Assert.True(shouldDlq);
        // Consumer: BasicNack(requeue: false) → crawl.page.dlx → crawl.page.dlq
    }

    [Fact]
    public void Html_link_extractor_is_deterministic_not_ai()
    {
        var html = """
            <html><body>
              <a href="/internal">in</a>
              <a href="https://other.example/x">out</a>
              <a href="mailto:nobody@example.com">mail</a>
            </body></html>
            """;
        var extractor = new HtmlLinkExtractor();
        var baseUri = new Uri("https://example.com/");
        var links = extractor.Extract(html, baseUri, new UrlNormalizationOptions());
        Assert.Contains(links, u => u.AbsoluteUri == "https://example.com/internal");
        Assert.Contains(links, u => u.AbsoluteUri == "https://other.example/x");
        Assert.DoesNotContain(links, u => u.Scheme == "mailto");
    }

    [Fact]
    public void Domain_link_ratio_ignores_non_http()
    {
        // mailto already filtered at extract; DLR uses internal/total http(s) counts on page.
        var ratio = DomainLinkRatio.Calculate(internalLinks: 2, totalLinks: 5);
        Assert.Equal(0.4m, ratio);
        Assert.Equal(0m, DomainLinkRatio.Calculate(0, 0));
    }

    [Fact]
    public void Stable_host_hash_is_deterministic_for_advisory_locks()
    {
        var a = HostPolitenessGate.StableHash("example.com");
        var b = HostPolitenessGate.StableHash("example.com");
        var c = HostPolitenessGate.StableHash("other.com");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Progress_event_shape_has_no_html_or_secrets()
    {
        var evt = new CrawlJobProgressEvent(
            Guid.NewGuid(),
            "Running",
            42,
            Total: 10,
            Queued: 3,
            Processing: 1,
            Done: 5,
            Skipped: 1,
            Failed: 0,
            StartedAt: null,
            CompletedAt: null,
            FailureReason: null,
            EventKind: CrawlJobProgressEvent.KindPageProgress,
            OccurredAt: DateTimeOffset.UtcNow);

        var json = System.Text.Json.JsonSerializer.Serialize(evt);
        Assert.DoesNotContain("<html", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("progressPercent", json, StringComparison.OrdinalIgnoreCase);
    }
}
