using Crawl.Domain.Links;
using Crawl.Infrastructure.Html;

namespace Crawl.Integration.Tests;

public class SparseHtmlAndSeedTests
{
    [Fact]
    public void Extract_PicksCanonicalAndAreaLinks()
    {
        const string html = """
            <!DOCTYPE html><html><head>
            <link rel="canonical" href="https://fixture.local/canonical-page"/>
            </head><body>
            <map><area href="/area-target" shape="rect"/></map>
            <a href="/about.html">About</a>
            </body></html>
            """;

        var extractor = new HtmlLinkExtractor();
        var page = new Uri("https://fixture.local/start");
        var links = extractor.Extract(html, page);

        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/canonical-page");
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/area-target");
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/about.html");
    }

    [Fact]
    public void ExtractNavigationHints_MetaRefreshTarget()
    {
        const string html = """
            <html><head>
            <meta http-equiv="refresh" content="0;url=https://fixture.local/after-refresh"/>
            </head></html>
            """;

        var hints = new HtmlLinkExtractor().ExtractNavigationHints(
            html,
            new Uri("https://fixture.local/"),
            UrlNormalizationOptions.Default);

        Assert.Contains(hints, u => u.AbsoluteUri == "https://fixture.local/after-refresh");
    }
}
