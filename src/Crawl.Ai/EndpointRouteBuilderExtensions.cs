using Crawl.Ai.Agent;
using Crawl.Ai.AskTheCrawl;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Incidents;
using Crawl.Ai.Integrations;
using Crawl.Ai.LoginAssist;
using Crawl.Ai.Models;
using Crawl.Ai.Options;
using Crawl.Ai.Summarization;
using Crawl.Domain.Incidents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Crawl.Ai;

/// <summary>
/// Additive minimal-API endpoints for Crawl.Api:
/// <c>app.MapCrawlAi();</c>
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCrawlAi(this IEndpointRouteBuilder app, string prefix = "/api/ai")
    {
        var group = app.MapGroup(prefix).WithTags("CrawlAi");

        group.MapGet("/features", (
            IOptions<CrawlAiOptions> options,
            IOptions<CrawlAlertOptions> alerts,
            IDeepSeekClient deepSeek) =>
        {
            var o = options.Value;
            return Results.Ok(new AiFeatureFlagsResponse(
                o.Enabled,
                o.Enabled && o.FieldFinderEnabled,
                o.Enabled && o.LoginAssistEnabled,
                o.Enabled && o.SummarizationEnabled,
                o.Enabled && o.AskTheCrawlEnabled,
                o.Enabled && o.AgentLoopEnabled,
                o.Enabled && o.IncidentMonitorEnabled,
                deepSeek.IsConfigured,
                alerts.Value.EmailEnabled));
        });

        group.MapGet("/jobs/{jobId:guid}/incidents", async (
            Guid jobId,
            IOptions<CrawlAiOptions> options,
            ICrawlIncidentMonitor monitor,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.IncidentMonitorEnabled)
            {
                return Results.Json(new { error = "incident_monitor_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var items = await monitor.ListForJobAsync(jobId, 20, ct).ConfigureAwait(false);
            var dtos = items.Select(ToDto).ToList();
            return Results.Ok(new JobIncidentsResponse(jobId, dtos.FirstOrDefault(), dtos));
        });

        group.MapPost("/jobs/{jobId:guid}/field-finder", async (
            Guid jobId,
            FieldFinderBody body,
            IOptions<CrawlAiOptions> options,
            IFieldFinderService service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.FieldFinderEnabled)
            {
                return Results.Json(new { error = "field_finder_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.SuggestAsync(
                new FieldFinderRequest(jobId, body.PageId, body.PageUrl, body.HtmlOrText ?? "", body.GoalHint),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/jobs/{jobId:guid}/login-assist/detect", async (
            Guid jobId,
            LoginDetectBody body,
            IOptions<CrawlAiOptions> options,
            ILoginAssistService service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.LoginAssistEnabled)
            {
                return Results.Json(new { error = "login_assist_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.DetectAsync(
                new LoginAssistDetectRequest(jobId, body.PageUrl, body.HtmlOrText ?? ""),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/jobs/{jobId:guid}/login-assist/confirm-step", async (
            Guid jobId,
            LoginConfirmBody body,
            IOptions<CrawlAiOptions> options,
            ILoginAssistService service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.LoginAssistEnabled)
            {
                return Results.Json(new { error = "login_assist_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.ConfirmStepAsync(
                new LoginAssistConfirmRequest(
                    jobId,
                    body.AuditEventId,
                    body.StepIndex,
                    body.Approved,
                    body.CredentialVaultRef,
                    body.OperatorId),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapGet("/jobs/{jobId:guid}/login-assist/audit", (
            Guid jobId,
            IOptions<CrawlAiOptions> options,
            ILoginAssistService service) =>
        {
            if (!options.Value.Enabled || !options.Value.LoginAssistEnabled)
            {
                return Results.Json(new { error = "login_assist_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(service.GetAuditTrail(jobId));
        });

        group.MapPost("/jobs/{jobId:guid}/summarize", async (
            Guid jobId,
            SummarizeBody body,
            IOptions<CrawlAiOptions> options,
            IResultSummarizer service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.SummarizationEnabled)
            {
                return Results.Json(new { error = "summarization_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.SummarizeAsync(
                new SummarizeRequest(
                    jobId,
                    body.SeedUrl ?? "",
                    body.Status ?? "Unknown",
                    body.PageCount,
                    body.EdgeCount,
                    body.Pages ?? Array.Empty<CrawlPageSnapshot>(),
                    body.Failures),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/jobs/{jobId:guid}/ask", async (
            Guid jobId,
            AskBody body,
            IOptions<CrawlAiOptions> options,
            IAskTheCrawlService service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.AskTheCrawlEnabled)
            {
                return Results.Json(new { error = "ask_the_crawl_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.AskAsync(
                new AskTheCrawlRequest(jobId, body.Question ?? "", body.Pages ?? Array.Empty<CrawlPageSnapshot>()),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/jobs/{jobId:guid}/agent/plan", async (
            Guid jobId,
            AgentPlanBody body,
            IOptions<CrawlAiOptions> options,
            IAgentOrchestrator service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.AgentLoopEnabled)
            {
                return Results.Json(new { error = "agent_loop_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.PlanAsync(
                new AgentPlanRequest(jobId, body.Goal ?? "", body.Pages, body.PageHtmlSnippet),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/jobs/{jobId:guid}/agent/confirm", async (
            Guid jobId,
            AgentConfirmBody body,
            IOptions<CrawlAiOptions> options,
            IAgentOrchestrator service,
            CancellationToken ct) =>
        {
            if (!options.Value.Enabled || !options.Value.AgentLoopEnabled)
            {
                return Results.Json(new { error = "agent_loop_disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var resp = await service.ConfirmAsync(
                new AgentConfirmRequest(body.PlanId, jobId, body.Approved, body.OperatorId, body.ApprovedToolNames),
                ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/integrations/payer/eligibility-stub", async (
            PayerEligibilityStubRequest body,
            IPayerPortalClient client,
            CancellationToken ct) =>
        {
            var resp = await client.CheckEligibilityAsync(body, ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        group.MapPost("/integrations/clearinghouse/claim-stub", async (
            ClearinghouseStubRequest body,
            IClearinghouseClient client,
            CancellationToken ct) =>
        {
            var resp = await client.SubmitClaimStubAsync(body, ct).ConfigureAwait(false);
            return Results.Ok(resp);
        });

        return app;
    }

    private static IncidentRemediationDto ToDto(CrawlIncident i) => new(
        i.Id,
        i.JobId,
        i.CorrelationId,
        i.FaultCode.ToString(),
        i.Severity.ToString(),
        i.Summary,
        i.Host,
        i.PageId,
        i.HttpStatus,
        i.RemediationKind.ToString(),
        i.RemediationStatus,
        i.RemediationDetail,
        i.UsedModel,
        i.AiAdvice,
        i.AlertSent,
        i.CreatedAt);

    public sealed record FieldFinderBody(Guid? PageId, string? PageUrl, string? HtmlOrText, string? GoalHint);
    public sealed record LoginDetectBody(string? PageUrl, string? HtmlOrText);
    public sealed record LoginConfirmBody(string AuditEventId, int StepIndex, bool Approved, string? CredentialVaultRef, string? OperatorId);
    public sealed record SummarizeBody(string? SeedUrl, string? Status, int PageCount, int EdgeCount, IReadOnlyList<CrawlPageSnapshot>? Pages, IReadOnlyList<string>? Failures);
    public sealed record AskBody(string? Question, IReadOnlyList<CrawlPageSnapshot>? Pages);
    public sealed record AgentPlanBody(string? Goal, IReadOnlyList<CrawlPageSnapshot>? Pages, string? PageHtmlSnippet);
    public sealed record AgentConfirmBody(Guid PlanId, bool Approved, string? OperatorId, IReadOnlyList<string>? ApprovedToolNames);
}
