using Crawl.Ai.Agent;
using Crawl.Ai.AskTheCrawl;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.LoginAssist;
using Crawl.Ai.Models;
using Crawl.Ai.Options;
using Crawl.Ai.Summarization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.Controllers;

/// <summary>
/// Additive MVC controller alternative for hosts that prefer controllers over minimal APIs.
/// Prefer <see cref="EndpointRouteBuilderExtensions.MapCrawlAi"/> when using minimal hosting.
/// </summary>
[ApiController]
[Route("api/ai")]
public sealed class CrawlAiController : ControllerBase
{
    private readonly CrawlAiOptions _options;
    private readonly IDeepSeekClient _deepSeek;
    private readonly IFieldFinderService _fieldFinder;
    private readonly ILoginAssistService _loginAssist;
    private readonly IResultSummarizer _summarizer;
    private readonly IAskTheCrawlService _ask;
    private readonly IAgentOrchestrator _agent;

    public CrawlAiController(
        IOptions<CrawlAiOptions> options,
        IDeepSeekClient deepSeek,
        IFieldFinderService fieldFinder,
        ILoginAssistService loginAssist,
        IResultSummarizer summarizer,
        IAskTheCrawlService ask,
        IAgentOrchestrator agent)
    {
        _options = options.Value;
        _deepSeek = deepSeek;
        _fieldFinder = fieldFinder;
        _loginAssist = loginAssist;
        _summarizer = summarizer;
        _ask = ask;
        _agent = agent;
    }

    [HttpGet("features")]
    public ActionResult<AiFeatureFlagsResponse> Features() =>
        Ok(new AiFeatureFlagsResponse(
            _options.Enabled,
            _options.Enabled && _options.FieldFinderEnabled,
            _options.Enabled && _options.LoginAssistEnabled,
            _options.Enabled && _options.SummarizationEnabled,
            _options.Enabled && _options.AskTheCrawlEnabled,
            _options.Enabled && _options.AgentLoopEnabled,
            _deepSeek.IsConfigured));

    [HttpPost("jobs/{jobId:guid}/field-finder")]
    public async Task<ActionResult<FieldFinderResponse>> FieldFinder(
        Guid jobId,
        [FromBody] FieldFinderRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.FieldFinderEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var req = body with { JobId = jobId };
        return Ok(await _fieldFinder.SuggestAsync(req, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/login-assist/detect")]
    public async Task<ActionResult<LoginAssistDetectResponse>> LoginDetect(
        Guid jobId,
        [FromBody] LoginAssistDetectRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.LoginAssistEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _loginAssist.DetectAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/login-assist/confirm-step")]
    public async Task<ActionResult<LoginAssistConfirmResponse>> LoginConfirm(
        Guid jobId,
        [FromBody] LoginAssistConfirmRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.LoginAssistEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _loginAssist.ConfirmStepAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/summarize")]
    public async Task<ActionResult<SummarizeResponse>> Summarize(
        Guid jobId,
        [FromBody] SummarizeRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.SummarizationEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _summarizer.SummarizeAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/ask")]
    public async Task<ActionResult<AskTheCrawlResponse>> Ask(
        Guid jobId,
        [FromBody] AskTheCrawlRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.AskTheCrawlEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _ask.AskAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/agent/plan")]
    public async Task<ActionResult<AgentPlanResponse>> AgentPlan(
        Guid jobId,
        [FromBody] AgentPlanRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.AgentLoopEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _agent.PlanAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }

    [HttpPost("jobs/{jobId:guid}/agent/confirm")]
    public async Task<ActionResult<AgentConfirmResponse>> AgentConfirm(
        Guid jobId,
        [FromBody] AgentConfirmRequest body,
        CancellationToken ct)
    {
        if (!_options.Enabled || !_options.AgentLoopEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(await _agent.ConfirmAsync(body with { JobId = jobId }, ct).ConfigureAwait(false));
    }
}
