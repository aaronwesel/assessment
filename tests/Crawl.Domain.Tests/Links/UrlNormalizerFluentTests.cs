using Crawl.Domain.Links;
using FluentAssertions;

namespace Crawl.Domain.Tests.Links;

/// <summary>
/// Assessment mastery edge cases: relative paths, query/fragment, mailto/tel, zero-link DLR.
/// </summary>
public class UrlNormalizerFluentTests
{
    [Fact]
    public void Relative_about_resolves_against_base()
    {
        var bas = new Uri("https://payer.example/portal/home");
        UrlNormalizer.TryNormalize("/about", bas, out var uri).Should().BeTrue();
        uri!.AbsoluteUri.Should().Be("https://payer.example/about");
    }

    [Fact]
    public void Query_ref_is_preserved_and_sorted()
    {
        UrlNormalizer.TryNormalize("https://example.com/x?ref=1&a=2", out var uri).Should().BeTrue();
        uri!.AbsoluteUri.Should().Be("https://example.com/x?a=2&ref=1");
    }

    [Fact]
    public void Fragment_section_is_stripped()
    {
        UrlNormalizer.TryNormalize("https://example.com/claims#section", out var uri).Should().BeTrue();
        uri!.AbsoluteUri.Should().Be("https://example.com/claims");
        uri.Fragment.Should().BeEmpty();
    }

    [Theory]
    [InlineData("mailto:billing@payer.example")]
    [InlineData("tel:+18005551212")]
    public void Anchor_schemes_mailto_tel_rejected(string href)
    {
        UrlNormalizer.TryNormalize(href, out _).Should().BeFalse();
    }

    [Fact]
    public void DomainLinkRatio_zero_outgoing_links_is_zero()
    {
        DomainLinkRatio.Calculate(0, 0).Should().Be(0m);
    }
}
