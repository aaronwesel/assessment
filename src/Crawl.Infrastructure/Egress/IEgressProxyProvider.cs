namespace Crawl.Infrastructure.Egress;

/// <summary>
/// Pluggable outbound egress for IP-block remediations (proxy pool / VPN-shaped hop).
/// Implementations must fail closed when no endpoints are configured.
/// </summary>
public interface IEgressProxyProvider
{
    /// <summary>True when at least one usable proxy URI is configured.</summary>
    bool HasProxies { get; }

    /// <summary>Current active proxy, or null when using direct egress.</summary>
    Uri? CurrentProxy { get; }

    /// <summary>
    /// Rotate to the next configured proxy. Returns false (fail closed) when none configured.
    /// </summary>
    bool TrySwitchProxy(out Uri? proxy, out string reason);

    /// <summary>Snapshot of configured endpoints (host:port only — no credentials in logs).</summary>
    IReadOnlyList<string> DescribeEndpoints();
}
