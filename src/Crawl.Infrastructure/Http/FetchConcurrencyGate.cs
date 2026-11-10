using Microsoft.Extensions.Options;

namespace Crawl.Infrastructure.Http;

/// <summary>Process-wide cap on concurrent HTML fetches (Scrapy CONCURRENT_REQUESTS inspired).</summary>
public sealed class FetchConcurrencyGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public FetchConcurrencyGate(IOptions<CrawlHttpOptions> options)
    {
        var limit = Math.Max(1, options.Value.MaxConcurrentFetches);
        _semaphore = new SemaphoreSlim(limit, limit);
    }

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
