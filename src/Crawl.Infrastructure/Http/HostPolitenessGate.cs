using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Http;

/// <summary>
/// Per-host spacing between fetches (Scrapy DOWNLOAD_DELAY / Crawl-delay inspired).
/// Supports incident remediations: politeness bump multipliers and scheduled backoff.
/// </summary>
public sealed class HostPolitenessGate(IOptions<CrawlHttpOptions> options)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _nextAllowedTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _politenessMultipliers = new(StringComparer.OrdinalIgnoreCase);

    public double GetPolitenessMultiplier(string host)
    {
        var key = host.ToLowerInvariant();
        return _politenessMultipliers.TryGetValue(key, out var m) ? m : 1.0;
    }

    /// <summary>Apply a per-host politeness bump (rate-limit remediation).</summary>
    public double BumpPoliteness(string host, double multiplier)
    {
        var key = host.ToLowerInvariant();
        var bump = multiplier < 1.0 ? 2.0 : multiplier;
        return _politenessMultipliers.AddOrUpdate(
            key,
            bump,
            (_, current) => Math.Min(current * bump, options.Value.MaxPolitenessMultiplier));
    }

    /// <summary>Schedule an explicit backoff window for the host (Retry-After / exponential).</summary>
    public void ScheduleBackoff(string host, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return;

        var key = host.ToLowerInvariant();
        var until = Environment.TickCount64 + (long)delay.TotalMilliseconds;
        _nextAllowedTicks.AddOrUpdate(key, until, (_, existing) => Math.Max(existing, until));
    }

    public async Task WaitAsync(string host, TimeSpan? robotsCrawlDelay, CancellationToken cancellationToken)
    {
        var key = host.ToLowerInvariant();
        var gate = _hostLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var baseDelay = robotsCrawlDelay
                ?? TimeSpan.FromMilliseconds(Math.Max(0, options.Value.PolitenessDelayMilliseconds));

            var multiplier = GetPolitenessMultiplier(key);
            var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier);

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
