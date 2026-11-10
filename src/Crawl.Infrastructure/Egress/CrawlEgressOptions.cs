namespace Crawl.Infrastructure.Egress;

/// <summary>
/// Operator-supplied HTTP(S) proxy / rotating egress endpoints.
/// Real VPN or residential IP hops require endpoints the operator configures — never bake paid VPN keys in repo.
/// </summary>
public sealed class CrawlEgressOptions
{
    public const string SectionName = "CrawlEgress";

    /// <summary>When true, HardBlock remediations may rotate through <see cref="Proxies"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// HTTP(S) proxy URLs (e.g. http://egress-1:8888). Empty = fail closed on proxy remediations.
    /// </summary>
    public List<string> Proxies { get; set; } = [];

    /// <summary>Optional note shown in docs/UI — no secrets.</summary>
    public string OperatorNote { get; set; } =
        "Supply HTTP(S) proxy or VPN-shaped hop endpoints via CrawlEgress:Proxies. Fail closed when empty.";
}
