namespace Crawl.Worker.Fetching;

public sealed class HtmlPageFetcher(IHttpClientFactory httpClientFactory, ILogger<HtmlPageFetcher> logger)
{
    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("crawl");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/html, application/xhtml+xml");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Transient fetch failure for {Url}", url);
            throw;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return FetchResult.Fail($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null
                && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase))
            {
                return FetchResult.Skip($"Non-HTML content-type: {mediaType}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return FetchResult.Ok(html);
        }
    }
}

public sealed record FetchResult(bool Success, bool Skipped, string? Html, string? Error)
{
    public static FetchResult Ok(string html) => new(true, false, html, null);
    public static FetchResult Skip(string reason) => new(false, true, null, reason);
    public static FetchResult Fail(string error) => new(false, false, null, error);
}
