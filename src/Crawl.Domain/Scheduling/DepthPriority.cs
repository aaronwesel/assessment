namespace Crawl.Domain.Scheduling;

/// <summary>
/// Maps crawl depth to RabbitMQ message priority so lower depths drain first (BFS-aware).
/// Priority 0 is lowest; brokers with x-max-priority prefer higher values.
/// </summary>
public static class DepthPriority
{
    public const byte MaxPriority = 10;

    public static byte ForDepth(int depth)
    {
        if (depth <= 0)
            return MaxPriority;
        var priority = MaxPriority - depth;
        return priority <= 0 ? (byte)1 : (byte)priority;
    }
}
