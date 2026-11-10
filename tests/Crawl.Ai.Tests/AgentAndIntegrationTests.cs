using Crawl.Ai.Agent;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Integrations;
using Crawl.Ai.Options;
using Crawl.Ai.Summarization;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Crawl.Ai.Tests;

public class AgentAndIntegrationTests
{
    [Fact]
    public async Task Agent_requires_human_confirm_before_tools()
    {
        var field = new FieldFinderService(new FakeDeepSeek(false), OptionsFactory.Create(new CrawlAiOptions()));
        var sum = new ResultSummarizer(new FakeDeepSeek(false));
        var agent = new AgentOrchestrator(field, sum);

        var plan = await agent.PlanAsync(new(Guid.NewGuid(), "find eligibility fields", null, null));
        Assert.True(plan.RequiresHumanConfirm);
        Assert.Contains("human-confirm", plan.RoleSignal);
        Assert.NotEmpty(plan.ProposedToolCalls);

        var declined = await agent.ConfirmAsync(new(plan.PlanId, plan.JobId, false, "op", null));
        Assert.Equal("declined", declined.Status);
        Assert.Empty(declined.ExecutedTools);

        var approved = await agent.ConfirmAsync(new(plan.PlanId, plan.JobId, true, "op", new[] { "propose_fields" }));
        Assert.Equal("executed", approved.Status);
        Assert.Contains("propose_fields", approved.ExecutedTools);
        Assert.DoesNotContain(approved.ExecutedTools, t => t.Contains("parse_link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Payer_stub_uses_field_finder()
    {
        var field = new FieldFinderService(new FakeDeepSeek(false), OptionsFactory.Create(new CrawlAiOptions()));
        var payer = new StubPayerPortalClient(field);
        var resp = await payer.CheckEligibilityAsync(new(
            "payer-a",
            "MEM123456",
            "1234567890",
            Guid.NewGuid(),
            "<input id='memberId' name='memberId' /><input name='npi' />"));

        Assert.Equal("stub_accepted", resp.Status);
        Assert.True(resp.RequiresOperatorConfirmation);
        Assert.Contains("***", resp.Message); // member masked
        Assert.DoesNotContain("MEM123456", resp.Message);
    }

    [Fact]
    public async Task Clearinghouse_stub_returns_correlation()
    {
        var ch = new StubClearinghouseClient();
        var resp = await ch.SubmitClaimStubAsync(new(
            "partner-1",
            "CLM-1",
            new Dictionary<string, string> { ["amount"] = "10.00" }));
        Assert.Equal("stub_queued", resp.Status);
        Assert.False(string.IsNullOrWhiteSpace(resp.CorrelationId));
    }
}
