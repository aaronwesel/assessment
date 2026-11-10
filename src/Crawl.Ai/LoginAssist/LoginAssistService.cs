using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Crawl.Ai.Models;

namespace Crawl.Ai.LoginAssist;

public interface ILoginAssistService
{
    Task<LoginAssistDetectResponse> DetectAsync(
        LoginAssistDetectRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginAssistConfirmResponse> ConfirmStepAsync(
        LoginAssistConfirmRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<LoginAssistAuditEvent> GetAuditTrail(Guid jobId);
}

/// <summary>
/// Assisted login scaffold: detect login forms, guided operator-approved steps.
/// Never logs passwords — vault references only. Every attempt is audited.
/// </summary>
public sealed class LoginAssistService : ILoginAssistService
{
    private static readonly Regex FormRegex = new(
        @"<form\b([^>]*)>(.*?)</form>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex InputRegex = new(
        @"<input\b([^>]*)/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttrRegex = new(
        @"(\w+)\s*=\s*[""']([^""']*)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ConcurrentDictionary<Guid, List<LoginAssistAuditEvent>> _audit = new();

    public Task<LoginAssistDetectResponse> DetectAsync(
        LoginAssistDetectRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var detection = DetectForm(request.HtmlOrText);
        var eventId = Guid.NewGuid().ToString("N");
        AppendAudit(new LoginAssistAuditEvent(
            eventId,
            request.JobId,
            "login_form_detected",
            DateTimeOffset.UtcNow,
            OperatorId: null,
            Detail: detection.Detected
                ? $"Login form detected; fields={detection.Fields.Count}; passwordFields={detection.Fields.Count(f => f.IsPassword)}"
                : "No login form detected",
            CredentialVaultRef: null));

        // Explicit hygiene: never persist raw HTML passwords into audit.
        return Task.FromResult(new LoginAssistDetectResponse(request.JobId, detection, eventId));
    }

    public Task<LoginAssistConfirmResponse> ConfirmStepAsync(
        LoginAssistConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(request.CredentialVaultRef)
            && LooksLikeRawSecret(request.CredentialVaultRef))
        {
            AppendAudit(new LoginAssistAuditEvent(
                Guid.NewGuid().ToString("N"),
                request.JobId,
                "login_step_rejected_raw_secret",
                DateTimeOffset.UtcNow,
                request.OperatorId,
                "Rejected: credential payload looked like a raw secret; use vault ref only.",
                CredentialVaultRef: null));

            return Task.FromResult(new LoginAssistConfirmResponse(
                request.JobId,
                request.AuditEventId,
                request.StepIndex,
                false,
                "rejected",
                "Passwords must never be submitted — provide a credential vault reference only."));
        }

        var status = request.Approved ? "approved" : "declined";
        AppendAudit(new LoginAssistAuditEvent(
            Guid.NewGuid().ToString("N"),
            request.JobId,
            $"login_step_{status}",
            DateTimeOffset.UtcNow,
            request.OperatorId,
            $"Step {request.StepIndex} {status} (parent={request.AuditEventId})",
            request.CredentialVaultRef));

        var message = request.Approved
            ? $"Step {request.StepIndex} approved. Next guided action may proceed under operator control."
            : $"Step {request.StepIndex} declined by operator.";

        return Task.FromResult(new LoginAssistConfirmResponse(
            request.JobId,
            request.AuditEventId,
            request.StepIndex,
            request.Approved,
            status,
            message));
    }

    public IReadOnlyList<LoginAssistAuditEvent> GetAuditTrail(Guid jobId)
    {
        return _audit.TryGetValue(jobId, out var list)
            ? list.ToList()
            : Array.Empty<LoginAssistAuditEvent>();
    }

    internal static LoginFormDetection DetectForm(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return EmptyDetection();
        }

        foreach (Match form in FormRegex.Matches(html))
        {
            var formAttrs = ParseAttrs(form.Groups[1].Value);
            var body = form.Groups[2].Value;
            var fields = new List<LoginFormField>();
            var hasPassword = false;
            var hasUser = false;

            foreach (Match input in InputRegex.Matches(body))
            {
                var attrs = ParseAttrs(input.Groups[1].Value);
                var type = attrs.GetValueOrDefault("type", "text");
                var name = attrs.GetValueOrDefault("name") ?? attrs.GetValueOrDefault("id") ?? "input";
                var isPassword = string.Equals(type, "password", StringComparison.OrdinalIgnoreCase);
                if (isPassword)
                {
                    hasPassword = true;
                }

                if (IsUsernameField(name, type))
                {
                    hasUser = true;
                }

                if (string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                fields.Add(new LoginFormField(
                    name,
                    type,
                    attrs.GetValueOrDefault("placeholder") ?? attrs.GetValueOrDefault("aria-label"),
                    isPassword));
            }

            if (!hasPassword)
            {
                continue;
            }

            var steps = new List<string>
            {
                "Confirm this is the intended payer/portal login page.",
                "Select credential vault reference (never paste passwords into chat/logs).",
                "Approve session cookie / token handoff after successful auth.",
                "Resume crawl with operator-approved authenticated session."
            };

            if (!hasUser)
            {
                steps.Insert(1, "Identify username/member-id field manually — heuristic did not find one.");
            }

            return new LoginFormDetection(
                true,
                formAttrs.GetValueOrDefault("action"),
                formAttrs.GetValueOrDefault("method") ?? "post",
                fields,
                steps);
        }

        // Fallback: password input outside a well-formed form.
        if (html.Contains("type=\"password\"", StringComparison.OrdinalIgnoreCase)
            || html.Contains("type='password'", StringComparison.OrdinalIgnoreCase))
        {
            return new LoginFormDetection(
                true,
                null,
                "post",
                new[] { new LoginFormField("password", "password", null, true) },
                new[]
                {
                    "Password field detected without a clear form wrapper — confirm page manually.",
                    "Use credential vault reference only.",
                    "Approve handoff before continuing crawl."
                });
        }

        return EmptyDetection();
    }

    private static LoginFormDetection EmptyDetection() =>
        new(false, null, null, Array.Empty<LoginFormField>(), Array.Empty<string>());

    private static bool IsUsernameField(string name, string type)
    {
        if (string.Equals(type, "email", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var n = name.ToLowerInvariant();
        return n.Contains("user") || n.Contains("email") || n.Contains("login") || n.Contains("member");
    }

    private static bool LooksLikeRawSecret(string value)
    {
        if (value.StartsWith("vault:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("secret:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject anything that looks like a typed password rather than a ref.
        return value.Length >= 6 && !value.Contains(':');
    }

    private static Dictionary<string, string> ParseAttrs(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttrRegex.Matches(raw))
        {
            dict[m.Groups[1].Value] = m.Groups[2].Value;
        }

        return dict;
    }

    private void AppendAudit(LoginAssistAuditEvent evt)
    {
        var list = _audit.GetOrAdd(evt.JobId, _ => new List<LoginAssistAuditEvent>());
        lock (list)
        {
            list.Add(evt);
        }
    }
}
