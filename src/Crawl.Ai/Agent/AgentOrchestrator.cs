using System.Collections.Concurrent;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Models;
using Crawl.Ai.Summarization;

namespace Crawl.Ai.Agent;

public interface IAgentOrchestrator
{
    Task<AgentPlanResponse> PlanAsync(AgentPlanRequest request, CancellationToken cancellationToken = default);

    Task<AgentConfirmResponse> ConfirmAsync(AgentConfirmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Small agentic tool-loop stub with mandatory human confirm.
/// Role signal: low-level agentic AI orchestration for RCM portal ops — does not own the crawl broker.
/// </summary>
public sealed class AgentOrchestrator : IAgentOrchestrator
{
    public const string RoleSignal =
        "assistive-agentic-orchestrator: plan→tool-propose→human-confirm (never owns crawl graph / Domain Link Ratio)";

    private readonly ConcurrentDictionary<Guid, AgentPlanResponse> _plans = new();
    private readonly IFieldFinderService _fieldFinder;
    private readonly IResultSummarizer _summarizer;

    public AgentOrchestrator(IFieldFinderService fieldFinder, IResultSummarizer summarizer)
    {
        _fieldFinder = fieldFinder;
        _summarizer = summarizer;
    }

    public Task<AgentPlanResponse> PlanAsync(AgentPlanRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tools = new List<AgentToolCall>
        {
            new("fetch_page_meta", new Dictionary<string, string>
            {
                ["jobId"] = request.JobId.ToString()
            }, "Load page metadata already persisted by the .NET crawl worker."),
            new("propose_fields", new Dictionary<string, string>
            {
                ["jobId"] = request.JobId.ToString(),
                ["goal"] = request.Goal
            }, "Suggest extraction targets for operator confirmation."),
            new("summarize_job", new Dictionary<string, string>
            {
                ["jobId"] = request.JobId.ToString()
            }, "Summarize crawl outcomes without recomputing Domain Link Ratio.")
        };

        var steps = new List<string>
        {
            "Inspect operator goal and available page snapshots.",
            "Propose tool calls (meta fetch, field finder, summarize).",
            "Wait for human confirm before executing any tool.",
            "Return tool results for operator review."
        };

        var planId = Guid.NewGuid();
        var plan = new AgentPlanResponse(
            request.JobId,
            planId,
            RoleSignal,
            steps,
            tools,
            RequiresHumanConfirm: true);

        _plans[planId] = plan;
        return Task.FromResult(plan);
    }

    public async Task<AgentConfirmResponse> ConfirmAsync(
        AgentConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_plans.TryGetValue(request.PlanId, out var plan) || plan.JobId != request.JobId)
        {
            return new AgentConfirmResponse(
                request.PlanId,
                request.JobId,
                "not_found",
                Array.Empty<string>(),
                new[] { "Unknown plan id." },
                RoleSignal);
        }

        if (!request.Approved)
        {
            return new AgentConfirmResponse(
                request.PlanId,
                request.JobId,
                "declined",
                Array.Empty<string>(),
                new[] { "Operator declined agent plan; no tools executed." },
                RoleSignal);
        }

        var allowed = new HashSet<string>(
            request.ApprovedToolNames ?? plan.ProposedToolCalls.Select(t => t.ToolName).ToList(),
            StringComparer.OrdinalIgnoreCase);

        var executed = new List<string>();
        var results = new List<string>();

        foreach (var tool in plan.ProposedToolCalls.Where(t => allowed.Contains(t.ToolName)))
        {
            executed.Add(tool.ToolName);
            results.Add(await ExecuteToolAsync(tool, request.JobId, cancellationToken).ConfigureAwait(false));
        }

        return new AgentConfirmResponse(
            request.PlanId,
            request.JobId,
            "executed",
            executed,
            results,
            RoleSignal);
    }

    private async Task<string> ExecuteToolAsync(AgentToolCall tool, Guid jobId, CancellationToken ct)
    {
        switch (tool.ToolName)
        {
            case "fetch_page_meta":
                // Stub: real meta comes from Crawl.Api / domain — agent must not scrape links.
                return "page_meta: deferred to Crawl.Api job/pages endpoint (AI does not parse links).";

            case "propose_fields":
            {
                tool.Arguments.TryGetValue("html", out var htmlArg);
                tool.Arguments.TryGetValue("goal", out var goalArg);
                var html = htmlArg ?? "<form><input name='memberId' /><input name='npi' /></form>";
                var resp = await _fieldFinder.SuggestAsync(
                    new FieldFinderRequest(jobId, null, null, html, goalArg),
                    ct).ConfigureAwait(false);
                return $"propose_fields: {resp.Suggestions.Count} suggestion(s); confirm required={resp.RequiresOperatorConfirmation}.";
            }

            case "summarize_job":
            {
                var resp = await _summarizer.SummarizeAsync(
                    new SummarizeRequest(
                        jobId,
                        "(from agent)",
                        "Unknown",
                        0,
                        0,
                        Array.Empty<CrawlPageSnapshot>(),
                        Array.Empty<string>()),
                    ct).ConfigureAwait(false);
                return $"summarize_job: {resp.Summary}";
            }

            default:
                return $"unknown_tool: {tool.ToolName}";
        }
    }
}
