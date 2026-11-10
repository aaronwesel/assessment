using Crawl.Domain.Robots;

namespace Crawl.Domain.Tests.Robots;

public class RobotsSitemapTests
{
    [Fact]
    public void Parse_CollectsSitemapDirectives()
    {
        const string robots = """
            User-agent: *
            Disallow: /cart

            Sitemap: https://example.com/sitemap-index.xml
            Sitemap: https://example.com/sitemap-2.xml
            """;

        var rules = RobotsTxtRules.Parse(robots);

        Assert.Equal(2, rules.Sitemaps.Count);
        Assert.Contains("https://example.com/sitemap-index.xml", rules.Sitemaps);
    }
}
