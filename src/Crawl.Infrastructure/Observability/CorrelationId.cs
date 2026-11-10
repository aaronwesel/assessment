namespace Crawl.Infrastructure.Observability;

/// <summary>
/// Ambient correlation for HTTP → Serilog → RabbitMQ/MassTransit headers (RCM-style audit trail).
/// </summary>
public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";
    public const string MassTransitHeader = "CorrelationId";

    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current
    {
        get => CurrentValue.Value;
        set => CurrentValue.Value = value;
    }

    public static string Ensure()
    {
        if (!string.IsNullOrWhiteSpace(Current))
            return Current!;

        Current = Guid.NewGuid().ToString("N");
        return Current;
    }
}
