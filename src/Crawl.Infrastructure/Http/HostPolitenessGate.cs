using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Http;

/// <summary>
/// Per-host spacing between fetches (Scrapy DOWNLOAD_DELAY / Crawl-delay inspired).
/// </summary>
public sealed class HostPolitenessGate(IOptions<CrawlHttpOptions> options)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _nextAllowedTicks = new(StringComparer.OrdinalIgnoreCase);

    public async Task WaitAsync(string host, TimeSpan? robotsCrawlDelay, CancellationToken cancellationToken)
    {
        var key = host.ToLowerInvariant();
        var gate = _hostLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var delay = robotsCrawlDelay
                ?? TimeSpan.FromMilliseconds(Math.Max(0, options.Value.PolitenessDelayMilliseconds));

            var now = Environment.TickCount64;
            if (_nextAllowedTicks.TryGetValue(key, out var next) && next > now)
            {
                var wait = (int)Math.Min(int.MaxValue, next - now);
                if (wait > 0)
                    await Task.Delay(wait, cancellationToken);
            }

            _nextAllowedTicks[key] = Environment.TickCount64 + (long)delay.TotalMilliseconds;
        }
        finally
        {
            gate.Release();
        }
    }
}
