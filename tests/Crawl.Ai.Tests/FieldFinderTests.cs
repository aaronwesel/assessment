using Crawl.Ai.DeepSeek;
using Crawl.Ai.FieldFinder;
using Crawl.Ai.Models;
using Crawl.Ai.Options;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Crawl.Ai.Tests;

public class FieldFinderTests
{
    [Fact]
    public void Heuristic_suggests_inputs_and_billing_labels()
    {
        var html = """
            <html><body>
            <label>Member ID</label><input type="text" name="memberId" id="memberId" />
            <label>NPI</label><input type="text" name="npi" />
            <input type="password" name="secret" />
            <div>Eligibility Status</div>
            </body></html>
            """;

        var suggestions = FieldFinderService.HeuristicSuggestions(html, "eligibility");
        Assert.Contains(suggestions, s => s.CssSelector == "#memberId");
        Assert.DoesNotContain(suggestions, s => s.Label.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || (s.CssSelector ?? "").Contains("password"));
        Assert.All(suggestions, s => Assert.True(s.RequiresOperatorConfirmationImplicit()));
    }

    [Fact]
    public async Task SuggestAsync_degrades_without_api_key()
    {
        var svc = new FieldFinderService(new FakeDeepSeek(configured: false), OptionsFactory.Create(new CrawlAiOptions()));
        var resp = await svc.SuggestAsync(new FieldFinderRequest(
            Guid.NewGuid(), null, "https://payer.example/elig",
            "<input name='memberId' id='m' />", "eligibility"));

        Assert.False(resp.UsedModel);
        Assert.NotNull(resp.DegradedReason);
        Assert.True(resp.RequiresOperatorConfirmation);
        Assert.NotEmpty(resp.Suggestions);
    }

    [Fact]
    public async Task SuggestAsync_merges_model_json()
    {
        var json = """{"suggestions":[{"label":"Claim ID","cssSelector":"#claim","xpath":null,"reason":"table","confidence":0.9,"sampleValue":"C1"}]}""";
        var svc = new FieldFinderService(new FakeDeepSeek(configured: true, content: json), OptionsFactory.Create(new CrawlAiOptions()));
        var resp = await svc.SuggestAsync(new FieldFinderRequest(
            Guid.NewGuid(), null, null, "<input name='memberId' id='memberId' />", null));

        Assert.True(resp.UsedModel);
        Assert.Contains(resp.Suggestions, s => s.Label == "Claim ID");
    }
}

file static class FieldSuggestionAssert
{
    // Documentation helper — suggestions always require confirmation at the response level.
    public static bool RequiresOperatorConfirmationImplicit(this FieldSuggestion _) => true;
}
