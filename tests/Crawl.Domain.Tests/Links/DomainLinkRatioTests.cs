using Crawl.Domain.Links;

namespace Crawl.Domain.Tests.Links;

public class DomainLinkRatioTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(3, 3, 1)]
    [InlineData(1, 4, 0.25)]
    [InlineData(2, 5, 0.4)]
    public void Calculate_ReturnsExpectedRatio(int internalLinks, int totalLinks, decimal expected)
    {
        Assert.Equal(expected, DomainLinkRatio.Calculate(internalLinks, totalLinks));
    }

    [Fact]
    public void Calculate_ThrowsWhenInternalExceedsTotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainLinkRatio.Calculate(3, 2));
    }

    [Fact]
    public void Calculate_ThrowsOnNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainLinkRatio.Calculate(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainLinkRatio.Calculate(0, -1));
    }
}
