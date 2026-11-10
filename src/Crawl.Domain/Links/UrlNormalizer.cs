using System.Diagnostics.CodeAnalysis;

namespace Crawl.Domain.Links;

/// <summary>
/// Canonical URL form for de-duplication within a job (deterministic; never AI-assisted):
/// absolute http(s) only; fragment and user-info stripped; scheme/host lowercased (IDN → punycode);
/// default ports removed; empty path becomes "/"; optional query-parameter sort; path casing preserved.
/// </summary>
public static class UrlNormalizer
{
    public static bool TryNormalize(string? url, [NotNullWhen(true)] out Uri? normalized) =>
        TryNormalize(url, baseUri: null, UrlNormalizationOptions.Default, out normalized);

    public static bool TryNormalize(string? url, Uri? baseUri, [NotNullWhen(true)] out Uri? normalized) =>
        TryNormalize(url, baseUri, UrlNormalizationOptions.Default, out normalized);

    public static bool TryNormalize(
        string? url,
        Uri? baseUri,
        UrlNormalizationOptions options,
        [NotNullWhen(true)] out Uri? normalized)
    {
        ArgumentNullException.ThrowIfNull(options);
        normalized = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        Uri? parsed = baseUri is null
            ? Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) ? absolute : null
            : Uri.TryCreate(baseUri, trimmed, out var resolved) ? resolved : null;

        if (parsed is null || !IsHttp(parsed) || string.IsNullOrEmpty(parsed.Host))
            return false;

        // IdnHost lowercases ASCII and converts unicode hosts to punycode.
        var host = parsed.IdnHost.ToLowerInvariant();
        if (string.IsNullOrEmpty(host))
            return false;

        var builder = new UriBuilder
        {
            Scheme = parsed.Scheme.ToLowerInvariant(),
            Host = host,
            Path = string.IsNullOrEmpty(parsed.AbsolutePath) ? "/" : parsed.AbsolutePath,
            Fragment = string.Empty,
            // Clear credentials — never persist user-info in the crawl graph.
            UserName = string.Empty,
            Password = string.Empty
        };

        // Strip default ports (80/http, 443/https); keep non-default ports.
        if (parsed.IsDefaultPort)
            builder.Port = -1;
        else
            builder.Port = parsed.Port;

        builder.Query = options.SortQueryParameters
            ? SortQuery(parsed.Query)
            : TrimLeadingQuestion(parsed.Query);

        Uri candidate;
        try
        {
            candidate = builder.Uri;
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (candidate.AbsoluteUri.Length > options.MaxUrlLength)
            return false;

        normalized = candidate;
        return true;
    }

    public static bool IsSameHost(Uri left, Uri right) =>
        string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stable query canonicalization: drop leading '?', sort name/value pairs, rebuild without '?'.
    /// Duplicate keys are preserved after sort (order among equal names is by value).
    /// </summary>
    internal static string SortQuery(string? rawQuery)
    {
        var trimmed = TrimLeadingQuestion(rawQuery);
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var idx = part.IndexOf('=');
                if (idx < 0)
                    return (Name: Decode(part), Value: string.Empty, Raw: part);
                return (Name: Decode(part[..idx]), Value: Decode(part[(idx + 1)..]), Raw: part);
            })
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Value, StringComparer.Ordinal)
            .Select(p => p.Raw);

        return string.Join('&', pairs);
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static string TrimLeadingQuestion(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return string.Empty;
        return query[0] == '?' ? query[1..] : query;
    }

    private static bool IsHttp(Uri uri) =>
        uri.Scheme is "http" or "https"
        || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
