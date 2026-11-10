using System.Collections.Concurrent;
using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Domain.Pages;
using Crawl.Infrastructure.Html;
using Crawl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crawl.Integration.Tests;

public class UrlDedupStressTests
{
    [Fact]
    public void ConcurrentNormalization_CollapsesEquivalentUrls()
    {
        var variants = new[]
        {
            "https://EXAMPLE.com:443/a?b=1&a=2#x",
            "https://example.com/a?a=2&b=1",
            "HTTPS://Example.com/a?b=1&a=2",
            "https://user:pw@example.com/a?a=2&b=1#zzz"
        };

        var set = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        Parallel.ForEach(variants.Concat(variants).Concat(variants), v =>
        {
            Assert.True(UrlNormalizer.TryNormalize(v, out var uri));
            set.TryAdd(uri!.AbsoluteUri, 0);
        });

        Assert.Single(set);
        Assert.Equal("https://example.com/a?a=2&b=1", set.Keys.Single());
    }

    [Fact]
    public void Extractor_DedupsQueryOrderAndFragments_FromFixtureEdges()
    {
        var html = """
            <html><body>
              <a href="/page?b=1&a=2">one</a>
              <a href="/page?a=2&b=1#frag">two</a>
              <a href="https://EXTERNAL.example/x?z=1&y=2">ext</a>
              <a href="https://external.example/x?y=2&z=1">ext2</a>
              <a href="mailto:x@y.z">mail</a>
            </body></html>
            """;

        var links = new HtmlLinkExtractor().Extract(html, new Uri("https://fixture.local/"));
        Assert.Equal(2, links.Count);
        Assert.Contains(links, u => u.AbsoluteUri == "https://fixture.local/page?a=2&b=1");
        Assert.Contains(links, u => u.AbsoluteUri == "https://external.example/x?y=2&z=1");
    }

    [Fact]
    public void EfModel_DeclaresUniqueIndexes_ForPagesAndEdges()
    {
        var options = new DbContextOptionsBuilder<CrawlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new CrawlDbContext(options);
        var pageEntity = db.Model.FindEntityType(typeof(CrawlPage));
        var edgeEntity = db.Model.FindEntityType(typeof(PageEdge));
        Assert.NotNull(pageEntity);
        Assert.NotNull(edgeEntity);

        var pageUnique = pageEntity!.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(["JobId", "Url"], pageUnique.Properties.Select(p => p.Name).ToArray());

        var edgeUnique = edgeEntity!.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(["SourcePageId", "TargetUrl"], edgeUnique.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task ChildEnqueue_KnownSet_RejectsDuplicateNormalizedUrls()
    {
        var job = CrawlJob.Create(new Uri("https://fixture.local/"), 2, 50, DateTimeOffset.UtcNow);
        var known = new HashSet<string>(StringComparer.Ordinal) { job.Url };
        var candidates = new[]
        {
            "https://FIXTURE.local/about",
            "https://fixture.local/about#x",
            "https://fixture.local:443/about?b=1&a=2",
            "https://fixture.local/about?a=2&b=1"
        };

        var inserted = 0;
        foreach (var raw in candidates)
        {
            Assert.True(UrlNormalizer.TryNormalize(raw, out var uri));
            if (!known.Add(uri!.AbsoluteUri))
                continue;
            inserted++;
        }

        Assert.Equal(2, inserted); // /about and /about?a=2&b=1
        await Task.CompletedTask;
    }
}
