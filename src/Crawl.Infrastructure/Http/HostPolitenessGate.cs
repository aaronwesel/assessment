using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Crawl.Infrastructure.Http;

/// <summary>
/// Per-host spacing between fetches (Scrapy DOWNLOAD_DELAY / Crawl-delay inspired).
/// Process-local gate plus optional Postgres advisory lock so concurrent workers
/// on the same host do not stampede when scaled out on a shared queue.
/// </summary>
public sealed class HostPolitenessGate(
    IOptions<CrawlHttpOptions> options,
    ILogger<HostPolitenessGate> logger,
    NpgsqlDataSource? dataSource = null)
{
    // dataSource optional so unit tests can exercise process-local politeness without Postgres.
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
            // Cross-worker serialization for the same host (best-effort; falls back to local-only).
            await using var distributed = await TryAcquireDistributedLockAsync(key, cancellationToken);

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

    private async Task<IAsyncDisposable?> TryAcquireDistributedLockAsync(
        string hostKey,
        CancellationToken cancellationToken)
    {
        if (!options.Value.DistributedHostPoliteness || dataSource is null)
            return null;

        try
        {
            var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            // Stable 64-bit key from host string; two-key advisory lock namespace "crawl-host".
            var lockKey = StableHash(hostKey);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT pg_advisory_lock(872014, @k)";
                cmd.Parameters.AddWithValue("k", lockKey);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return new AdvisoryLockRelease(connection, lockKey, logger);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Distributed host politeness lock unavailable for {Host}; using process-local only", hostKey);
            return null;
        }
    }

    /// <summary>Deterministic non-cryptographic hash suitable for advisory lock keys.</summary>
    public static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            // Avoid 0 which can collide with unused keys; keep in int range.
            var asInt = (int)(hash & 0x7FFFFFFF);
            return asInt == 0 ? 1 : asInt;
        }
    }

    private sealed class AdvisoryLockRelease(
        NpgsqlConnection connection,
        int lockKey,
        ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT pg_advisory_unlock(872014, @k)";
                    cmd.Parameters.AddWithValue("k", lockKey);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to release host advisory lock {Key}", lockKey);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
