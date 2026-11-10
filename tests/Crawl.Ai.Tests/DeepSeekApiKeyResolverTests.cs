using Crawl.Ai.DeepSeek;

namespace Crawl.Ai.Tests;

public class DeepSeekApiKeyResolverTests
{
    private readonly List<(string Key, string? Prior)> _restore = [];

    [Fact]
    public void Resolve_prefers_configured_value()
    {
        Assert.Equal("from-config", DeepSeekApiKeyResolver.Resolve("from-config"));
    }

    [Fact]
    public void Resolve_reads_file_env_when_direct_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deepseek-{Guid.NewGuid():N}.key");
        File.WriteAllText(path, "file-secret\n");
        try
        {
            SetEnv("DEEPSEEK_API_KEY", null);
            SetEnv("CrawlAi__DeepSeekApiKey", null);
            SetEnv("DEEPSEEK_API_KEY_FILE", path);
            Assert.Equal("file-secret", DeepSeekApiKeyResolver.Resolve(null));
        }
        finally
        {
            File.Delete(path);
            RestoreEnv();
        }
    }

    [Fact]
    public void AiDegradedCopy_never_mentions_raw_env_keys()
    {
        var fields = typeof(AiDegradedCopy)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!);

        foreach (var text in fields)
        {
            Assert.DoesNotContain("DEEPSEEK_API_KEY", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CrawlAi__", text, StringComparison.Ordinal);
        }
    }

    private void SetEnv(string key, string? value)
    {
        _restore.Add((key, Environment.GetEnvironmentVariable(key)));
        if (value is null)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
        else
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void RestoreEnv()
    {
        foreach (var (key, prior) in _restore)
        {
            Environment.SetEnvironmentVariable(key, prior);
        }

        _restore.Clear();
    }
}
