using Crawl.Domain.Robots;

namespace Crawl.Domain.Tests.Robots;

public class RobotsTxtRulesTests
{
    private const string Agent = "CrawlAssessmentBot";

    [Fact]
    public void Parse_Empty_AllowsEverything()
    {
        var rules = RobotsTxtRules.Parse(null);
        Assert.True(rules.IsAllowed(Agent, new Uri("https://example.com/any")));
        Assert.Null(rules.GetCrawlDelay(Agent));
    }

    [Fact]
    public void Disallow_BlocksMatchingPrefix()
    {
        var rules = RobotsTxtRules.Parse("""
            User-agent: *
            Disallow: /private
            Allow: /private/public
            Crawl-delay: 1.5
            """);

        Assert.False(rules.IsAllowed(Agent, new Uri("https://example.com/private/secret")));
        Assert.True(rules.IsAllowed(Agent, new Uri("https://example.com/private/public/x")));
        Assert.True(rules.IsAllowed(Agent, new Uri("https://example.com/ok")));
        Assert.Equal(TimeSpan.FromSeconds(1.5), rules.GetCrawlDelay(Agent));
    }

    [Fact]
    public void SpecificUserAgent_BeatsStar()
    {
        var rules = RobotsTxtRules.Parse("""
            User-agent: *
            Disallow: /

            User-agent: CrawlAssessmentBot
            Disallow: /admin
            Allow: /
            """);

        Assert.True(rules.IsAllowed(Agent, new Uri("https://example.com/index")));
        Assert.False(rules.IsAllowed(Agent, new Uri("https://example.com/admin/x")));
        Assert.False(rules.IsAllowed("OtherBot", new Uri("https://example.com/index")));
    }

    [Fact]
    public void EmptyDisallow_MeansAllowAllForThatDirective()
    {
        var rules = RobotsTxtRules.Parse("""
            User-agent: *
            Disallow:
            """);

        Assert.True(rules.IsAllowed(Agent, new Uri("https://example.com/anything")));
    }

    [Fact]
    public void ConsecutiveUserAgents_ShareRules()
    {
        var rules = RobotsTxtRules.Parse("""
            User-agent: googlebot
            User-agent: CrawlAssessmentBot
            Disallow: /tmp
            """);

        Assert.False(rules.IsAllowed(Agent, new Uri("https://example.com/tmp/x")));
        Assert.False(rules.IsAllowed("Googlebot", new Uri("https://example.com/tmp/x")));
    }
}
