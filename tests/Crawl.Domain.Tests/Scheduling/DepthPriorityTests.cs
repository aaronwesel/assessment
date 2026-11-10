using Crawl.Domain.Scheduling;

namespace Crawl.Domain.Tests.Scheduling;

public class DepthPriorityTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 9)]
    [InlineData(2, 8)]
    [InlineData(9, 1)]
    [InlineData(100, 1)]
    public void ForDepth_PrefersShallowerPages(int depth, byte expected)
    {
        Assert.Equal(expected, DepthPriority.ForDepth(depth));
    }

    [Fact]
    public void ForDepth_IsMonotonicNonIncreasing()
    {
        byte prev = DepthPriority.ForDepth(0);
        for (var d = 1; d <= 12; d++)
        {
            var next = DepthPriority.ForDepth(d);
            Assert.True(next <= prev);
            prev = next;
        }
    }
}
