using Crawl.Domain.Jobs;
using Crawl.Domain.Pages;

namespace Crawl.Api.Jobs;

/// <summary>
/// Deterministic operator hints for Job Details — complements React heuristics with worker truth (errors, robots, sparse HTML).
/// </summary>
public static class CrawlJobDiagnostics
{
    public static IReadOnlyList<string> Build(CrawlJob job, IReadOnlyList<CrawlPage> pages)
    {
        var hints = new List<string>();
        var root = pages.FirstOrDefault(p => p.Depth == 0 && p.ParentPageId is null);
        var done = pages.Count(p => p.Status == CrawlPageStatus.Done);
        var skipped = pages.Count(p => p.Status == CrawlPageStatus.Skipped);
        var robotsSkips = pages.Count(p =>
            p.Status == CrawlPageStatus.Skipped
            && p.Error is not null
            && p.Error.Contains("robots", StringComparison.OrdinalIgnoreCase));

        if (root?.Status == CrawlPageStatus.Skipped && root.Error is { } rootErr)
        {
            hints.Add(rootErr);
        }
        else if (root?.Status == CrawlPageStatus.Done
                 && root.LinkCount is 0 or null
                 && job.MaxDepth > 0)
        {
            hints.Add("Seed parsed with zero links — likely JS-rendered HTML or a block page masked as HTTP 200.");
        }

        if (robotsSkips > 0)
        {
            hints.Add(
                $"{robotsSkips} page(s) skipped by robots.txt — this is expected on marketplaces (e.g. eBay disallows most paths for non-search bots).");
        }

        if (job.Status == CrawlJobStatus.Completed
            && job.MaxDepth >= 1
            && job.MaxPages > 3
            && pages.Count <= 3
            && done > 0)
        {
            hints.Add(
                "Completed with only a few pages — common on JS-heavy sites, anti-bot shells, or strict robots rules. See crawl-readme § SPA / sparse HTML limits.");
        }

        if (skipped > 0 && hints.All(h => !h.Contains("JS shell", StringComparison.OrdinalIgnoreCase)))
        {
            var sparseSkip = pages.FirstOrDefault(p =>
                p.Error is not null
                && p.Error.Contains("JS shell", StringComparison.OrdinalIgnoreCase));
            if (sparseSkip?.Error is { } err)
                hints.Add(err);
        }

        if (job.FailureReason is { } reason)
            hints.Add($"Job failure: {reason}");

        return hints.Distinct(StringComparer.Ordinal).ToList();
    }
}
