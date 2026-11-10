using Crawl.Domain.Jobs;
using Crawl.Domain.Links;
using Crawl.Domain.Pages;

namespace Crawl.Api.Jobs;

/// <summary>Deterministic sparse-crawl hints for job summary (no AI).</summary>
public static class CrawlJobDiagnostics
{
    public static IReadOnlyList<string> BuildHints(CrawlJob job, IReadOnlyList<CrawlPage> pages)
    {
        if (pages.Count == 0)
            return [];

        var hints = new List<string>();
        var root = pages.FirstOrDefault(p => p.Depth == 0);
        var terminalJob = job.Status.IsTerminal();
        var sparse = pages.Count < Math.Max(8, job.MaxPages / 20) && job.MaxDepth >= 1;

        if (root?.LinkCount is >= 15 && root.InternalLinkCount is int internalCount)
        {
            var ratio = (double)internalCount / root.LinkCount;
            if (sparse && terminalJob && ratio < 0.2)
            {
                hints.Add(
                    "Seed HTML contains many links but few were treated as same-site — common causes: www vs bare domain, " +
                    "login/sign-in subdomains before host matching fix, or mostly off-site navigation.");
            }
        }

        if (sparse && terminalJob && root?.LinkCount is < 5 && pages.Count <= 3)
        {
            hints.Add(
                "Very few links in static HTML — the page may be JavaScript-rendered (SPA). This crawler does not execute JS; " +
                "try a deeper folder URL, sitemap, or a static documentation site.");
        }

        if (root is not null && !CrawlSiteHost.IsSameSite(new Uri(root.Url), job.StartHost))
        {
            try
            {
                var rootHost = new Uri(root.Url).IdnHost;
                if (!string.Equals(rootHost, job.StartHost, StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(
                        $"Seed host is {job.StartHost} but the stored root URL uses {rootHost}. Prefer https://{rootHost}/ " +
                        "when the site redirects apex → www.");
                }
            }
            catch (UriFormatException)
            {
                // ignore
            }
        }

        if (sparse && terminalJob && job.MaxDepth <= 1)
        {
            hints.Add(
                "Crawl depth is shallow — use Advanced options or “Try again with more options” to raise max depth (up to 5).");
        }

        return hints;
    }
}
