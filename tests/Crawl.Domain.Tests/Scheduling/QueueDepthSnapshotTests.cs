namespace Crawl.Domain.Tests.Scheduling;

/// <summary>Pure scale-signal heuristics (mirrors RabbitMqQueueStats snapshot helpers).</summary>
public class QueueDepthSuggestLogicTests
{
    private static bool SuggestScaleOut(uint messagesReady, uint consumerCount, uint depthPerConsumer = 20) =>
        messagesReady > 0
        && (consumerCount == 0 || messagesReady >= depthPerConsumer * Math.Max(1u, consumerCount));

    private static bool SuggestScaleIn(uint messagesReady, uint consumerCount, uint idleThreshold = 0) =>
        messagesReady <= idleThreshold && consumerCount > 1;

    [Fact]
    public void Scale_out_when_depth_high_vs_consumers()
    {
        Assert.True(SuggestScaleOut(messagesReady: 100, consumerCount: 2));
        Assert.True(SuggestScaleOut(messagesReady: 5, consumerCount: 0));
        Assert.False(SuggestScaleOut(messagesReady: 5, consumerCount: 2));
    }

    [Fact]
    public void Scale_in_when_queue_drained_and_extra_consumers()
    {
        Assert.True(SuggestScaleIn(messagesReady: 0, consumerCount: 3));
        Assert.False(SuggestScaleIn(messagesReady: 0, consumerCount: 1));
        Assert.False(SuggestScaleIn(messagesReady: 10, consumerCount: 3));
    }
}
