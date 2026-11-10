using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Crawl.Domain.Incidents;

/// <summary>
/// Deterministic severity + fault-code classification from HTTP status / exception signals.
/// No model calls — safe for unit tests and worker hot path.
/// </summary>
public static partial class CrawlFaultClassifier
{
    private static readonly Regex CaptchaHeuristic = CaptchaPattern();
    private static readonly Regex BlockHeuristic = BlockPattern();

    public static CrawlFaultEvent ClassifyHttp(
        Guid jobId,
        string correlationId,
        int statusCode,
        string? host = null,
        long? pageId = null,
        int? retryAfterSeconds = null,
        string? responseSnippet = null,
        int attempt = 1)
    {
        var code = statusCode switch
        {
            429 => CrawlFaultCode.RateLimited,
            503 => CrawlFaultCode.ServiceUnavailable,
            408 => CrawlFaultCode.Timeout,
            401 or 403 when LooksLikeCaptchaOrBlock(responseSnippet) => CrawlFaultCode.CaptchaOrBlock,
            401 or 403 => CrawlFaultCode.HardBlock,
            >= 500 => CrawlFaultCode.ServiceUnavailable,
            _ => CrawlFaultCode.OtherTransient
        };

        var severity = ClassifySeverity(code, attempt, retryAfterSeconds);
        var summary = BuildHttpSummary(code, statusCode, host, retryAfterSeconds);
        return new CrawlFaultEvent(
            jobId,
            correlationId,
            code,
            severity,
            summary,
            host,
            pageId,
            statusCode,
            retryAfterSeconds,
            attempt);
    }

    public static CrawlFaultEvent ClassifyException(
        Guid jobId,
        string correlationId,
        Exception exception,
        string? host = null,
        long? pageId = null,
        int attempt = 1)
    {
        var (code, detail) = MapException(exception);
        var severity = ClassifySeverity(code, attempt, retryAfterSeconds: null);
        var summary = SanitizeSummary($"{code}: {detail}" + (host is null ? "" : $" host={host}"));
        return new CrawlFaultEvent(
            jobId,
            correlationId,
            code,
            severity,
            summary,
            host,
            pageId,
            HttpStatus: null,
            RetryAfterSeconds: null,
            attempt);
    }

    public static CrawlFaultSeverity ClassifySeverity(
        CrawlFaultCode code,
        int attempt,
        int? retryAfterSeconds)
    {
        return code switch
        {
            CrawlFaultCode.HardBlock or CrawlFaultCode.CaptchaOrBlock => CrawlFaultSeverity.Critical,
            CrawlFaultCode.RepeatedDlq or CrawlFaultCode.JobFailed => CrawlFaultSeverity.Error,
            CrawlFaultCode.RateLimited when attempt >= 3 || retryAfterSeconds is > 60
                => CrawlFaultSeverity.Error,
            CrawlFaultCode.RateLimited => CrawlFaultSeverity.Warning,
            CrawlFaultCode.ServiceUnavailable when attempt >= 3 => CrawlFaultSeverity.Error,
            CrawlFaultCode.ServiceUnavailable => CrawlFaultSeverity.Warning,
            CrawlFaultCode.Timeout or CrawlFaultCode.DnsFailure or CrawlFaultCode.ConnectionReset
                => attempt >= 3 ? CrawlFaultSeverity.Error : CrawlFaultSeverity.Warning,
            CrawlFaultCode.OtherTransient => CrawlFaultSeverity.Warning,
            _ => CrawlFaultSeverity.Info
        };
    }

    public static bool LooksLikeCaptchaOrBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return CaptchaHeuristic.IsMatch(text) || BlockHeuristic.IsMatch(text);
    }

    public static bool LooksLikeHardBlock(string? text) =>
        LooksLikeCaptchaOrBlock(text)
        || (!string.IsNullOrWhiteSpace(text)
            && text.Contains("access denied", StringComparison.OrdinalIgnoreCase));

    private static (CrawlFaultCode Code, string Detail) MapException(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException!)
        {
            if (ex is TaskCanceledException or TimeoutException or OperationCanceledException)
                return (CrawlFaultCode.Timeout, "request timed out");

            if (ex is SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    SocketError.HostNotFound or SocketError.NoData or SocketError.NoRecovery
                        => (CrawlFaultCode.DnsFailure, "DNS resolution failed"),
                    SocketError.ConnectionReset or SocketError.NetworkReset
                        => (CrawlFaultCode.ConnectionReset, "connection reset"),
                    _ => (CrawlFaultCode.OtherTransient, $"socket {socket.SocketErrorCode}")
                };
            }

            if (ex is HttpRequestException http)
            {
                var msg = http.Message ?? string.Empty;
                if (msg.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("nodename nor servname", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such host", StringComparison.OrdinalIgnoreCase))
                {
                    return (CrawlFaultCode.DnsFailure, "DNS resolution failed");
                }

                if (msg.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase))
                {
                    return (CrawlFaultCode.ConnectionReset, "connection reset");
                }

                if (http.StatusCode is HttpStatusCode.TooManyRequests)
                    return (CrawlFaultCode.RateLimited, "HTTP 429");
                if (http.StatusCode is HttpStatusCode.ServiceUnavailable)
                    return (CrawlFaultCode.ServiceUnavailable, "HTTP 503");
            }

            var text = ex.Message ?? string.Empty;
            if (text.Contains("Transient HTTP 429", StringComparison.OrdinalIgnoreCase))
                return (CrawlFaultCode.RateLimited, "HTTP 429");
            if (text.Contains("Transient HTTP 503", StringComparison.OrdinalIgnoreCase))
                return (CrawlFaultCode.ServiceUnavailable, "HTTP 503");
        }

        return (CrawlFaultCode.OtherTransient, Truncate(exception.GetType().Name));
    }

    private static string BuildHttpSummary(
        CrawlFaultCode code,
        int statusCode,
        string? host,
        int? retryAfterSeconds)
    {
        var parts = new List<string> { $"{code}", $"HTTP {statusCode}" };
        if (!string.IsNullOrWhiteSpace(host))
            parts.Add($"host={host}");
        if (retryAfterSeconds is int s)
            parts.Add($"retry-after={s}s");
        return SanitizeSummary(string.Join(" ", parts));
    }

    /// <summary>Strip emails / credential-looking tokens from operator-facing summaries.</summary>
    public static string SanitizeSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return "crawl fault";

        var cleaned = EmailPattern().Replace(summary, "[redacted]");
        cleaned = PasswordPattern().Replace(cleaned, "[redacted]");
        return Truncate(cleaned.Trim());
    }

    private static string Truncate(string value, int max = 240) =>
        value.Length <= max ? value : value[..max] + "…";

    [GeneratedRegex(@"captcha|cf-challenge|hcaptcha|recaptcha|challenge-platform", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CaptchaPattern();

    [GeneratedRegex(@"\b(blocked|bot.?detected|unusual traffic|please verify you are human)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockPattern();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?i)(password|passwd|secret|api[_-]?key)\s*[:=]\s*\S+")]
    private static partial Regex PasswordPattern();
}
