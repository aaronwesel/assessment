using Crawl.Domain.Links;

namespace Crawl.Domain.Tests.Links;

public class CrawlSiteHostTests
{
    [Theory]
    [InlineData("www.ebay.com", "ebay.com", true)]
    [InlineData("ebay.com", "www.ebay.com", true)]
    [InlineData("signin.ebay.com", "ebay.com", true)]
    [InlineData("www.ebay.com", "www.ebay.com", true)]
    [InlineData("evil-ebay.com", "ebay.com", false)]
    [InlineData("notebay.com", "ebay.com", false)]
    [InlineData("example.com", "example.org", false)]
    public void IsSameSite_HostPairs(string linkHost, string rootHost, bool expected) =>
        Assert.Equal(expected, CrawlSiteHost.IsSameSite(linkHost, rootHost));

    [Fact]
    public void IsSameSite_Uri_uses_idn_host()
    {
        var uri = new Uri("https://www.ebay.com/deals");
        Assert.True(CrawlSiteHost.IsSameSite(uri, "ebay.com"));
    }
}
