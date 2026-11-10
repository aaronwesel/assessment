using Crawl.Ai.DeepSeek;

namespace Crawl.Ai.Tests;

internal sealed class FakeDeepSeek : IDeepSeekClient
{
    private readonly string? _content;

    public FakeDeepSeek(bool configured, string? content = "ok")
    {
        IsConfigured = configured;
        _content = content;
    }

    public bool IsConfigured { get; }

    public Task<DeepSeekResult> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new DeepSeekResult(null, false, AiDegradedCopy.OfflineHeuristics));
        }

        return Task.FromResult(new DeepSeekResult(_content, true, null));
    }
}

public class HardRuleGuardTests
{
    [Fact]
    public void Crawl_Ai_source_must_not_own_domain_link_ratio_or_link_parser()
    {
        var root = FindRepoRoot();
        var aiDir = Path.Combine(root, "src", "Crawl.Ai");
        Assert.True(Directory.Exists(aiDir), $"missing {aiDir}");

        var banned = new[]
        {
            "class DomainLinkRatio",
            "ComputeDomainLinkRatio",
            "UrlNormalizer",
            "ParseLinks(",
            "ExtractHref",
            "HtmlLinkParser"
        };

        foreach (var file in Directory.EnumerateFiles(aiDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var b in banned)
            {
                Assert.DoesNotContain(b, text);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Assessment.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "src", "Crawl.Ai")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root");
    }
}
