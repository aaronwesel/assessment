using System.Text.RegularExpressions;

namespace Crawl.Domain.Html;

/// <summary>
/// Heuristics for JS shells, bot blocks, and sparse HTML that yields false-empty crawls.
/// Deterministic only — never AI-assisted.
/// </summary>
public static partial class SparseHtmlSignals
{
    private static readonly Regex MetaRobotsNoIndex = MetaRobotsPattern();

    /// <summary>
    /// Page body looks like an anti-bot or application shell with little server-rendered navigation.
    /// </summary>
    public static bool LooksLikeJsShellOrBotBlock(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return true;

        if (html.Length < 900
            && !html.Contains("<a ", StringComparison.OrdinalIgnoreCase)
            && !html.Contains("<area ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (html.Contains("Something went wrong on our end", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Error Page |", StringComparison.OrdinalIgnoreCase)
            || html.Contains("enable JavaScript", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Please enable cookies", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TitleLooksLikeError(html))
            return true;

        if (MetaRobotsNoIndex.IsMatch(html) && html.Length < 12_000)
        {
            var anchorCount = CountOccurrences(html, "<a ");
            if (anchorCount < 2)
                return true;
        }

        return Incidents.CrawlFaultClassifier.LooksLikeHardBlock(html);
    }

    /// <summary>
    /// Fetch succeeded but the graph cannot expand — treat as suspicious when combined with <paramref name="extractedLinkCount"/>.
    /// </summary>
    public static bool IsSuspiciouslySparse(string? html, int extractedLinkCount)
    {
        if (extractedLinkCount > 2)
            return false;

        if (string.IsNullOrWhiteSpace(html))
            return true;

        if (extractedLinkCount == 0 && LooksLikeJsShellOrBotBlock(html))
            return true;

        return extractedLinkCount <= 1
               && html.Length < 4_096
               && !html.Contains("<a href", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleLooksLikeError(string html)
    {
        var titleStart = html.IndexOf("<title", StringComparison.OrdinalIgnoreCase);
        if (titleStart < 0)
            return false;
        var titleEnd = html.IndexOf("</title>", titleStart, StringComparison.OrdinalIgnoreCase);
        if (titleEnd < 0)
            return false;
        var title = html[(titleStart + 6)..titleEnd];
        return title.Contains("error", StringComparison.OrdinalIgnoreCase)
               && (title.Contains("page", StringComparison.OrdinalIgnoreCase)
                   || title.Contains("blocked", StringComparison.OrdinalIgnoreCase));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [GeneratedRegex(
        "<meta[^>]+name=[\"']robots[\"'][^>]+content=[\"'][^\"']*noindex",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaRobotsPattern();
}
