using Crawl.Domain.Html;

namespace Crawl.Domain.Tests.Html;

public class SparseHtmlSignalsTests
{
    [Fact]
    public void LooksLikeJsShellOrBotBlock_DetectsEbayStyleErrorPage()
    {
        const string html = """
            <!DOCTYPE html><html><head>
            <meta name="robots" content="noindex,nofollow"><title>Error Page | eBay</title></head>
            <body><p>Something went wrong on our end</p></body></html>
            """;

        Assert.True(SparseHtmlSignals.LooksLikeJsShellOrBotBlock(html));
        Assert.True(SparseHtmlSignals.IsSuspiciouslySparse(html, extractedLinkCount: 0));
    }

    [Fact]
    public void LooksLikeJsShellOrBotBlock_AllowsNormalPageWithAnchors()
    {
        const string html = """
            <!DOCTYPE html><html><body>
            <a href="/one">One</a><a href="/two">Two</a><a href="/three">Three</a>
            <p>Welcome to our static site with plenty of server-rendered links for crawlers.</p>
            </body></html>
            """;

        Assert.False(SparseHtmlSignals.LooksLikeJsShellOrBotBlock(html));
        Assert.False(SparseHtmlSignals.IsSuspiciouslySparse(html, extractedLinkCount: 3));
    }
}
