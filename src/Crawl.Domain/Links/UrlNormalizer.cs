using System.Diagnostics.CodeAnalysis;

namespace Crawl.Domain.Links;

/// <summary>
/// Canonical URL form for de-duplication within a job:
/// absolute http(s), fragment and user-info stripped, scheme/host lowercased,
/// default ports removed, empty path becomes "/". Path and query kept as-is.
/// </summary>
public static class UrlNormalizer
{
    private const UriComponents CanonicalComponents =
        UriComponents.SchemeAndServer | UriComponents.PathAndQuery;

    public static bool TryNormalize(string? url, [NotNullWhen(true)] out Uri? normalized) =>
        TryNormalize(url, baseUri: null, out normalized);

    public static bool TryNormalize(string? url, Uri? baseUri, [NotNullWhen(true)] out Uri? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        Uri? parsed = baseUri is null
            ? Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) ? absolute : null
            : Uri.TryCreate(baseUri, trimmed, out var resolved) ? resolved : null;

        if (parsed is null || !IsHttp(parsed) || string.IsNullOrEmpty(parsed.Host))
            return false;

        normalized = new Uri(parsed.GetComponents(CanonicalComponents, UriFormat.UriEscaped));
        return true;
    }

    public static bool IsSameHost(Uri left, Uri right) =>
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase);

    private static bool IsHttp(Uri uri) =>
        uri.Scheme is "http" or "https";
}
