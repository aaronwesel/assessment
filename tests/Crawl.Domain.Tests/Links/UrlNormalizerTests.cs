using Crawl.Domain.Links;

namespace Crawl.Domain.Tests.Links;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://Example.com/Path", "https://example.com/Path")]
    [InlineData("HTTPS://Example.COM/Path", "https://example.com/Path")]
    [InlineData("https://example.com:443/a", "https://example.com/a")]
    [InlineData("http://example.com:80/", "http://example.com/")]
    [InlineData("http://example.com:8080/a", "http://example.com:8080/a")]
    [InlineData("https://example.com/a#section", "https://example.com/a")]
    [InlineData("https://user:pass@example.com/x", "https://example.com/x")]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("  https://example.com/a  ", "https://example.com/a")]
    public void TryNormalize_Absolute_CanonicalizesSchemeHostPortFragmentUserInfo(string input, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(input, out var uri));
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://example.com/search?b=2&a=1", "https://example.com/search?a=1&b=2")]
    [InlineData("https://example.com/search?a=2&a=1&b=0", "https://example.com/search?a=1&a=2&b=0")]
    [InlineData("https://example.com/x?", "https://example.com/x")]
    [InlineData("https://example.com/x?utm_source=a&id=1", "https://example.com/x?id=1&utm_source=a")]
    public void TryNormalize_SortsQueryParametersByDefault(string input, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(input, out var uri));
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_CanPreserveQueryOrderWhenDisabled()
    {
        var options = new UrlNormalizationOptions { SortQueryParameters = false };
        Assert.True(UrlNormalizer.TryNormalize("https://example.com/x?b=2&a=1", null, options, out var uri));
        Assert.Equal("https://example.com/x?b=2&a=1", uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_Relative_ResolvesAgainstBase()
    {
        var baseUri = new Uri("https://example.com/docs/index.html");
        Assert.True(UrlNormalizer.TryNormalize("../about.html", baseUri, out var uri));
        Assert.Equal("https://example.com/about.html", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("page.html", "https://example.com/docs/guide/page.html")]
    [InlineData("./page.html", "https://example.com/docs/guide/page.html")]
    [InlineData("../api/", "https://example.com/docs/api/")]
    [InlineData("/about", "https://example.com/about")]
    [InlineData("//cdn.example.org/lib", "https://cdn.example.org/lib")]
    [InlineData("?q=1", "https://example.com/docs/guide/intro.html?q=1")]
    public void TryNormalize_ResolvesRelativeEdgeCases(string href, string expected)
    {
        var page = new Uri("https://example.com/docs/guide/intro.html");
        Assert.True(UrlNormalizer.TryNormalize(href, page, out var uri));
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_FragmentOnly_IgnoresAsNonHttpTarget()
    {
        var baseUri = new Uri("https://example.com/page");
        Assert.True(UrlNormalizer.TryNormalize("#top", baseUri, out var uri));
        Assert.Equal("https://example.com/page", uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_DotSegmentsAndFragments_Collapse()
    {
        string[] variants =
        [
            "https://EXAMPLE.com:443/docs/../docs/a#x",
            "/docs/a",
            "a#y"
        ];
        var baseUri = new Uri("https://example.com/docs/");

        var normalized = variants
            .Select(v => UrlNormalizer.TryNormalize(v, baseUri, out var u) ? u!.AbsoluteUri : null)
            .Distinct()
            .ToList();

        Assert.Equal(["https://example.com/docs/a"], normalized);
    }

    [Fact]
    public void TryNormalize_QueryOrderVariants_CollapseWhenSorted()
    {
        var a = "https://Example.com/path?b=1&a=2#frag";
        var b = "https://example.com:443/path?a=2&b=1";
        Assert.True(UrlNormalizer.TryNormalize(a, out var left));
        Assert.True(UrlNormalizer.TryNormalize(b, out var right));
        Assert.Equal(left!.AbsoluteUri, right!.AbsoluteUri);
        Assert.Equal("https://example.com/path?a=2&b=1", left.AbsoluteUri);
    }

    [Theory]
    [InlineData("mailto:a@b.com")]
    [InlineData("tel:+15551212")]
    [InlineData("javascript:void(0)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("data:text/html,hi")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsNonHttpSchemesAndEmpty(string? input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out _));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("/relative/without/base")]
    [InlineData("not a url")]
    public void TryNormalize_RejectsNonAbsoluteWithoutBase(string url)
    {
        Assert.False(UrlNormalizer.TryNormalize(url, out _));
    }

    [Fact]
    public void TryNormalize_PreservesPathCasing()
    {
        Assert.True(UrlNormalizer.TryNormalize("https://EXAMPLE.com/Api?B=2&a=1", out var uri));
        // Ordinal name sort: 'B' < 'a'
        Assert.Equal("https://example.com/Api?B=2&a=1", uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_RejectsOverlongUrls()
    {
        var longPath = new string('a', 3000);
        Assert.False(UrlNormalizer.TryNormalize($"https://example.com/{longPath}", out _));
    }

    [Fact]
    public void IsSameHost_IgnoresCase()
    {
        var a = new Uri("https://Example.com/a");
        var b = new Uri("https://example.com/b");
        Assert.True(UrlNormalizer.IsSameHost(a, b));
    }
}
