using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Infrastructure.Html;

namespace Crawl.Integration.Tests;

public class HtmlFixturePipelineTests
{
    private static string FixtureRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixtures", "site"));

    [Fact]
    public void Extract_FromIndexFixture_ComputesDomainLinkRatio()
    {
        var html = File.ReadAllText(Path.Combine(FixtureRoot, "index.html"));
        var pageUrl = new Uri("https://fixture.local/");
        var extractor = new HtmlLinkExtractor();

        var links = extractor.Extract(html, pageUrl);

        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/about.html");
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/docs/guide.html");
        Assert.Contains(links, u => u.AbsoluteUri == "https://external.example/path");
        Assert.DoesNotContain(links, u => u.Scheme == "mailto");
        // Fragment-only and duplicate about.html collapse to the page URL / single about.
        Assert.Equal(1, links.Count(u => u.AbsoluteUri == "https://fixture.local/about.html"));

        var internalCount = links.Count(l =>
            string.Equals(l.Host, pageUrl.Host, StringComparison.OrdinalIgnoreCase));
        var ratio = DomainLinkRatio.Calculate(internalCount, links.Count);

        // about, docs/guide, fragment->home = 3 internal; external = 1 → 3/4
        Assert.Equal(0.75m, ratio);
    }

    [Fact]
    public void Extract_RelativeLinks_ResolveAndStayIdempotentAcrossPages()
    {
        var extractor = new HtmlLinkExtractor();
        var guideHtml = File.ReadAllText(Path.Combine(FixtureRoot, "docs", "guide.html"));
        var guideUrl = new Uri("https://fixture.local/docs/guide.html");

        var links = extractor.Extract(guideHtml, guideUrl);
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/about.html");
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/missing.html");

        var job = CrawlJob.Create(new Uri("https://fixture.local/"), maxDepth: 2, maxPages: 200, DateTimeOffset.UtcNow);
        Assert.Equal("fixture.local", job.StartHost);
        Assert.True(job.CanEnqueueChildrenOf(1));
        Assert.False(job.CanEnqueueChildrenOf(2));
    }

    [Fact]
    public void UrlNormalizer_AndExtractor_AgreeOnDedup()
    {
        var baseUri = new Uri("https://fixture.local/page");
        Assert.True(UrlNormalizer.TryNormalize("/a#1", baseUri, out var a));
        Assert.True(UrlNormalizer.TryNormalize("/a#2", baseUri, out var b));
        Assert.Equal(a!.AbsoluteUri, b!.AbsoluteUri);
    }

    [Fact]
    public void Extract_FromEdgesFixture_CanonicalizesHostQueryAndFragments()
    {
        var html = File.ReadAllText(Path.Combine(FixtureRoot, "edges.html"));
        var pageUrl = new Uri("https://fixture.local/edges.html");
        var links = new HtmlLinkExtractor().Extract(html, pageUrl);

        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/About.html");
        Assert.Equal(1, links.Count(u => u.AbsoluteUri.Equals("https://fixture.local/About.html", StringComparison.Ordinal)));
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/q?a=1&b=2");
        Assert.Equal(1, links.Count(u => u.AbsoluteUri.StartsWith("https://fixture.local/q?", StringComparison.Ordinal)));
        Assert.Contains(links, u => u.AbsoluteUri == "https://external.example/cdn");
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/docs/guide.html");
        Assert.DoesNotContain(links, u => u.Scheme is "mailto" or "javascript");
    }
}
