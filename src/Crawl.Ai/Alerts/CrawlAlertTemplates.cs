using System.Text.RegularExpressions;
using Crawl.Domain.Incidents;

namespace Crawl.Ai.Alerts;

/// <summary>PHI-free crawl alert copy. Mirrors platform SES template discipline.</summary>
public static partial class CrawlAlertTemplates
{
    public static (string Subject, string Body) RenderIncident(
        CrawlIncident incident,
        string alertCode)
    {
        if (!AlertCodePattern().IsMatch(alertCode))
            alertCode = "crawl_incident";

        var subject = $"[assessment] Crawl alert: {alertCode}";
        var body =
            $"alert_code={alertCode}\n" +
            $"occurred_at={incident.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
            $"job_id={incident.JobId:N}\n" +
            $"correlation_id={incident.CorrelationId}\n" +
            $"fault_code={incident.FaultCode}\n" +
            $"severity={incident.Severity}\n" +
            $"remediation={incident.RemediationKind}/{incident.RemediationStatus}\n" +
            $"summary={CrawlFaultClassifier.SanitizeSummary(incident.Summary)}\n" +
            "note=Credential and PHI fields are omitted from crawl alerts.\n";

        AssertNoPhi(body);
        return (subject, body);
    }

    public static void AssertNoPhi(string text)
    {
        if (EmailPattern().IsMatch(text))
            throw new InvalidOperationException("PHI forbidden in crawl alert body: email address");
        if (PasswordPattern().IsMatch(text))
            throw new InvalidOperationException("PHI forbidden in crawl alert body: credential-like token");
        if (SsnPattern().IsMatch(text))
            throw new InvalidOperationException("PHI forbidden in crawl alert body: SSN-like number");
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]{0,63}$")]
    private static partial Regex AlertCodePattern();

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?i)(password|passwd|secret|api[_-]?key)\s*[:=]\s*\S+")]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnPattern();
}
