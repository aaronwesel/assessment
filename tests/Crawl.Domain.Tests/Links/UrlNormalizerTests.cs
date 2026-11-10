using Crawl.Domain.Links;

namespace Crawl.Domain.Tests.Links;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://Example.com/Path", "https://example.com/Path")]
    [InlineData("https://example.com:443/a", "https://example.com/a")]
    [InlineData("http://example.com:80/", "http://example.com/")]
    [InlineData("https://example.com/a#section", "https://example.com/a")]
    [InlineData("https://user:pass@example.com/x", "https://example.com/x")]
    public void TryNormalize_Absolute_StripsFragmentUserInfoAndDefaultPort(string input, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(input, out var uri));
        Assert.Equal(expected, uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_Relative_ResolvesAgainstBase()
    {
        var baseUri = new Uri("https://example.com/docs/index.html");
        Assert.True(UrlNormalizer.TryNormalize("../about.html", baseUri, out var uri));
        Assert.Equal("https://example.com/about.html", uri!.AbsoluteUri);
    }

    [Fact]
    public void TryNormalize_FragmentOnly_IgnoresAsNonHttpTarget()
    {
        var baseUri = new Uri("https://example.com/page");
        Assert.True(UrlNormalizer.TryNormalize("#top", baseUri, out var uri));
        Assert.Equal("https://example.com/page", uri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("mailto:a@b.com")]
    [InlineData("tel:+15551212")]
    [InlineData("javascript:void(0)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalize_RejectsNonHttpSchemesAndEmpty(string? input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out _));
    }

    [Fact]
    public void TryNormalize_PreservesQueryAndPathCasing()
    {
        Assert.True(UrlNormalizer.TryNormalize("https://EXAMPLE.com/Api?B=2&a=1", out var uri));
        Assert.Equal("https://example.com/Api?B=2&a=1", uri!.AbsoluteUri);
    }
}
