using System.Net;
using System.Text.RegularExpressions;
using Crawl.Ai.DeepSeek;
using static Crawl.Ai.DeepSeek.AiDegradedCopy;
using Crawl.Ai.Models;
namespace Crawl.Ai.SetupGuide;

public sealed record SetupGuideRequest(string Url, int MaxDepth, int WizardStep, string? Question);

public sealed record SetupGuideResponse(
    string NormalizedUrl,
    bool UrlValid,
    bool? Reachable,
    int? HttpStatus,
    bool LikelyLoginWall,
    IReadOnlyList<string> Observations,
    string StepAdvice,
    string? ModelAnswer,
    bool UsedModel,
    string? DegradedReason);

public interface ISetupGuideService
{
    Task<SetupGuideResponse> PreviewAsync(SetupGuideRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pre-job crawl coach: HEAD/GET probe + heuristics. Link graph math stays in .NET worker — this only advises.
/// </summary>
public sealed class SetupGuideService(IHttpClientFactory httpClientFactory, IDeepSeekClient deepSeek) : ISetupGuideService
{
    private static readonly Regex LoginHints = new(
        @"\b(login|sign\s*in|password|username|auth)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<SetupGuideResponse> PreviewAsync(SetupGuideRequest request, CancellationToken cancellationToken = default)
    {
        var observations = new List<string>();
        var stepAdvice = StepCopy(request.WizardStep);

        if (!TryCoerceHttpUri(request.Url, out var uri))
        {
            observations.Add("We could not normalize that address — try example.com or a full https:// link.");
            return new SetupGuideResponse(request.Url, false, null, null, false, observations, stepAdvice, null, false,
                "Invalid URL");
        }
        bool? reachable = null;
        int? status = null;
        var likelyLogin = false;
        string? htmlSnippet = null;

        try
        {
            var client = httpClientFactory.CreateClient("crawl-setup-guide");
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            status = (int)response.StatusCode;
            reachable = response.IsSuccessStatusCode || status is 401 or 403;
            if (status is >= 400 and < 500 && status is not 401 and not 403)
            {
                observations.Add($"Server returned HTTP {status} — the page may block bots or need a different start URL.");
            }
            else if (status >= 500)
            {
                observations.Add($"Server error HTTP {status} — retry later or pick another host.");
            }
            else
            {
                observations.Add($"HTTP {status} — seed looks reachable from the worker network.");
            }

            if (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                htmlSnippet = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (htmlSnippet.Length > 12_000) htmlSnippet = htmlSnippet[..12_000];
                likelyLogin = LoginHints.IsMatch(htmlSnippet);
                if (likelyLogin)
                    observations.Add("Login/sign-in language detected — expect a shallow tree unless you use login assist after the crawl.");
            }
        }
        catch (TaskCanceledException)
        {
            observations.Add("Timed out reaching the site — it may be slow or blocking our probe.");
        }
        catch (HttpRequestException ex)
        {
            observations.Add($"Network error: {ex.Message}");
        }

        if (request.MaxDepth < 1)
            observations.Add("Depth 0 only crawls the start page — bump depth to discover internal links.");
        else if (request.MaxDepth >= 3)
            observations.Add($"Depth {request.MaxDepth} can explode page count — watch max pages and cost.");

        string? modelAnswer = null;
        var usedModel = false;
        string? degraded = null;

        if (!string.IsNullOrWhiteSpace(request.Question) && deepSeek.IsConfigured)
        {
            var system = """
                You are a friendly crawl setup coach for Trailmap Crawl.
                Advise operators about URL choice, depth, login walls, and cost.
                Never claim you computed Domain Link Ratio or parsed links — only the .NET worker does that.
                Keep answers under 120 words.
                """;
            var user = $"""
                URL: {uri.AbsoluteUri}
                Reachable: {reachable?.ToString() ?? "unknown"}
                HTTP: {status?.ToString() ?? "n/a"}
                MaxDepth: {request.MaxDepth}
                Wizard step: {request.WizardStep}
                Question: {request.Question}
                Observations: {string.Join("; ", observations)}
                HTML snippet (trimmed): {(htmlSnippet?.Length > 800 ? htmlSnippet[..800] : htmlSnippet ?? "")}
                """;
            var result = await deepSeek.CompleteAsync(system, user, cancellationToken).ConfigureAwait(false);
            usedModel = result.UsedModel;
            modelAnswer = result.Content;
            degraded = result.DegradedReason;
        }
        else if (!string.IsNullOrWhiteSpace(request.Question))
        {
            modelAnswer = HeuristicAnswer(request.Question, uri.AbsoluteUri, request.MaxDepth, likelyLogin);
            degraded = OfflineHeuristicAnswer;
        }

        return new SetupGuideResponse(
            uri.AbsoluteUri,
            true,
            reachable,
            status,
            likelyLogin,
            observations,
            stepAdvice,
            modelAnswer,
            usedModel,
            degraded);
    }

    private static string StepCopy(int step) => step switch
    {
        0 => "Enter the site you want to map. We will add https:// if you forget it.",
        1 => "We probe the seed page from the server so you know if it is reachable before spending pages.",
        2 => "Choose depth and page budget — this controls how wide the internal link tree grows.",
        3 => "Confirm per-page pricing, then start. You can open Advanced options for robots and login notes.",
        4 => "Watch live progress — the tree and Domain Link Ratio fill in as workers finish pages.",
        5 => "Export results or retry with deeper depth / login assist from the report.",
        _ => "Follow the numbered steps — each one explains what happens and why.",
    };

    private static string HeuristicAnswer(string question, string url, int depth, bool login)
    {
        var lower = question.ToLowerInvariant();
        if (lower.Contains("cost") || lower.Contains("price"))
            return "First 5,000 pages-with-results are free; then Stripe test per-page billing at the published rate. Estimate = price × expected pages-with-results, capped by max pages.";
        if (lower.Contains("depth"))
            return depth <= 1
                ? "Try depth 2 for demo sites like books.toscrape.com — depth 0 only visits the start URL."
                : $"Depth {depth} follows internal links that many hops from the seed. Raise it if the homepage has no links.";
        if (login || lower.Contains("login"))
            return "If the site needs a sign-in, run a normal crawl first, then use Assisted login on Job Details with an HTML snapshot — never paste passwords here.";
        return $"For {url}, use depth 2–3 on public HTML sites. Domain Link Ratio is computed in .NET after each page is parsed.";
    }

    private static bool TryCoerceHttpUri(string? raw, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains('.')
            && !trimmed.Contains(' '))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        uri = parsed;
        return true;
    }
}
