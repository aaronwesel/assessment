using System.Diagnostics;
using System.Net;
using Crawl.Domain.Fetching;
using Crawl.Domain.Links;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Robots;
using Crawl.Worker.Metrics;
using Microsoft.Extensions.Options;

namespace Crawl.Worker.Fetching;

public sealed class HtmlPageFetcher(
    IHttpClientFactory httpClientFactory,
    IRobotsTxtService robots,
    HostPolitenessGate politeness,
    FetchConcurrencyGate concurrency,
    IOptions<CrawlHttpOptions> options,
    ILogger<HtmlPageFetcher> logger)
{
    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!await robots.IsAllowedAsync(url, cancellationToken))
        {
            CrawlWorkerMetrics.PagesRobotsBlocked.Add(1);
            return FetchResult.Skip($"Blocked by robots.txt for {options.Value.UserAgentToken}");
        }

        var robotsDelay = await robots.GetCrawlDelayAsync(url, cancellationToken);
        await politeness.WaitAsync(url.IdnHost, robotsDelay, cancellationToken);

        await concurrency.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            return await FetchWithRedirectsAsync(url, cancellationToken);
        }
        finally
        {
            CrawlWorkerMetrics.FetchDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            concurrency.Release();
        }
    }

    private async Task<FetchResult> FetchWithRedirectsAsync(Uri startUrl, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("crawl");
        var current = startUrl;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current.AbsoluteUri };
        var maxRedirects = Math.Max(0, options.Value.MaxRedirects);

        for (var hop = 0; hop <= maxRedirects; hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.ParseAdd("text/html, application/xhtml+xml;q=0.9,*/*;q=0.1");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                logger.LogWarning(ex, "Transient fetch failure for {Url}", current);
                throw new TransientCrawlException($"Transient fetch failure for {current}", ex);
            }

            using (response)
            {
                var code = (int)response.StatusCode;

                if (IsRedirect(code))
                {
                    var location = response.Headers.Location;
                    if (location is null)
                        return FetchResult.Fail($"Redirect {code} without Location");

                    if (!UrlNormalizer.TryNormalize(
                            location.IsAbsoluteUri ? location.AbsoluteUri : location.OriginalString,
                            current,
                            out var next))
                    {
                        return FetchResult.Fail($"Redirect {code} to non-http(s) Location");
                    }

                    if (!visited.Add(next.AbsoluteUri))
                        return FetchResult.Fail($"Redirect loop detected at {next.AbsoluteUri}");

                    if (hop == maxRedirects)
                        return FetchResult.Fail($"Exceeded max redirects ({maxRedirects})");

                    // Re-check robots for the redirect target host/path.
                    if (!await robots.IsAllowedAsync(next, cancellationToken))
                    {
                        CrawlWorkerMetrics.PagesRobotsBlocked.Add(1);
                        return FetchResult.Skip($"Blocked by robots.txt after redirect to {next.AbsoluteUri}");
                    }

                    current = next;
                    continue;
                }

                if (code is 408 or 429 || code >= 500)
                {
                    throw new TransientCrawlException($"Transient HTTP {code} for {current}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return FetchResult.Fail($"HTTP {code} {response.ReasonPhrase}");
                }

                var contentType = response.Content.Headers.ContentType;
                var mediaType = contentType?.MediaType;
                var contentTypeHeader = contentType?.ToString();

                var maxBytes = Math.Max(1024, options.Value.MaxResponseBytes);
                if (response.Content.Headers.ContentLength is > 0 and var declared && declared > maxBytes)
                    return FetchResult.Skip($"Response too large: {declared} bytes");

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var body = await ReadAtMostAsync(stream, maxBytes, cancellationToken);
                if (body is null)
                    return FetchResult.Skip($"Response exceeded {maxBytes} bytes");

                var htmlMedia = HtmlContentDecoder.IsHtmlMediaType(mediaType);
                if (!htmlMedia)
                {
                    // Sniff when Content-Type is missing or generic (application/octet-stream).
                    if (mediaType is null
                        || mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                        || mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!HtmlContentDecoder.LooksLikeHtml(body))
                            return FetchResult.Skip($"Non-HTML content-type: {mediaType ?? "(none)"}");
                    }
                    else
                    {
                        return FetchResult.Skip($"Non-HTML content-type: {mediaType}");
                    }
                }

                var html = HtmlContentDecoder.Decode(body, contentTypeHeader);
                return FetchResult.Ok(html, current);
            }
        }

        return FetchResult.Fail($"Exceeded max redirects ({maxRedirects})");
    }

    private static bool IsRedirect(int code) =>
        code is (int)HttpStatusCode.MovedPermanently
            or (int)HttpStatusCode.Found
            or (int)HttpStatusCode.SeeOther
            or (int)HttpStatusCode.TemporaryRedirect
            or (int)HttpStatusCode.PermanentRedirect
            or 308
            or 307;

    private static async Task<byte[]?> ReadAtMostAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(Math.Min(maxBytes, 65_536));
        var chunk = new byte[81_920];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

public sealed record FetchResult(bool Success, bool Skipped, string? Html, string? Error, Uri? FinalUrl)
{
    public static FetchResult Ok(string html, Uri finalUrl) => new(true, false, html, null, finalUrl);
    public static FetchResult Skip(string reason) => new(false, true, null, reason, null);
    public static FetchResult Fail(string error) => new(false, false, null, error, null);
}
