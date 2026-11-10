using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Egress;

/// <summary>
/// Config-list HTTP(S) proxy / rotating endpoint provider. Fail closed when <see cref="CrawlEgressOptions.Proxies"/> is empty.
/// </summary>
public sealed class ConfigListEgressProxyProvider : IEgressProxyProvider
{
    private readonly IOptionsMonitor<CrawlEgressOptions> _options;
    private readonly ILogger<ConfigListEgressProxyProvider> _logger;
    private readonly ConcurrentQueue<Uri> _rotation = new();
    private Uri? _current;
    private readonly object _gate = new();

    public ConfigListEgressProxyProvider(
        IOptionsMonitor<CrawlEgressOptions> options,
        ILogger<ConfigListEgressProxyProvider> logger)
    {
        _options = options;
        _logger = logger;
        Rebuild();
        _options.OnChange(_ => Rebuild());
    }

    public bool HasProxies
    {
        get
        {
            lock (_gate)
                return _current is not null || !_rotation.IsEmpty;
        }
    }

    public Uri? CurrentProxy
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public bool TrySwitchProxy(out Uri? proxy, out string reason)
    {
        lock (_gate)
        {
            var endpoints = ParseEndpoints(_options.CurrentValue);
            if (!_options.CurrentValue.Enabled)
            {
                proxy = null;
                reason = "CrawlEgress disabled; refuse proxy hop (fail closed).";
                return false;
            }

            if (endpoints.Count == 0)
            {
                proxy = null;
                reason =
                    "No CrawlEgress:Proxies configured; refuse IP-hop remediation (fail closed). Operator must supply HTTP(S) proxy or VPN-shaped endpoints.";
                _logger.LogWarning("Egress proxy switch refused: no proxies configured");
                return false;
            }

            if (_rotation.IsEmpty)
            {
                foreach (var ep in endpoints)
                    _rotation.Enqueue(ep);
            }

            if (!_rotation.TryDequeue(out var next))
            {
                proxy = null;
                reason = "Proxy rotation exhausted unexpectedly (fail closed).";
                return false;
            }

            _current = next;
            // Keep rotating: re-enqueue for round-robin.
            _rotation.Enqueue(next);
            proxy = next;
            reason = $"Switched egress to {Describe(next)}";
            _logger.LogInformation("Egress proxy switched to {ProxyHost}", next.Host);
            return true;
        }
    }

    public IReadOnlyList<string> DescribeEndpoints() =>
        ParseEndpoints(_options.CurrentValue).Select(Describe).ToList();

    private void Rebuild()
    {
        lock (_gate)
        {
            while (_rotation.TryDequeue(out _))
            {
            }

            _current = null;
            foreach (var ep in ParseEndpoints(_options.CurrentValue))
                _rotation.Enqueue(ep);
        }
    }

    private static List<Uri> ParseEndpoints(CrawlEgressOptions options)
    {
        var list = new List<Uri>();
        foreach (var raw in options.Proxies ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
                continue;
            if (uri.Scheme is not ("http" or "https" or "socks4" or "socks5"))
                continue;
            list.Add(uri);
        }

        return list;
    }

    private static string Describe(Uri uri) =>
        string.IsNullOrEmpty(uri.UserInfo)
            ? $"{uri.Scheme}://{uri.Host}:{uri.Port}"
            : $"{uri.Scheme}://***@{uri.Host}:{uri.Port}";
}
