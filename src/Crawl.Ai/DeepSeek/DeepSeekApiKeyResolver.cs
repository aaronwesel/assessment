namespace Crawl.Ai.DeepSeek;

/// <summary>
/// Resolves DeepSeek credentials from configuration, env, or *_FILE mounts (never shown in UI copy).
/// </summary>
public static class DeepSeekApiKeyResolver
{
    private static readonly string[] FileEnvKeys =
    [
        "DEEPSEEK_API_KEY_FILE",
        "CrawlAi__DeepSeekApiKey_FILE",
    ];

    private static readonly string[] DirectEnvKeys =
    [
        "DEEPSEEK_API_KEY",
        "CrawlAi__DeepSeekApiKey",
    ];

    public static string? Resolve(string? configuredKey)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            return configuredKey.Trim();
        }

        foreach (var key in DirectEnvKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        foreach (var fileKey in FileEnvKeys)
        {
            var path = Environment.GetEnvironmentVariable(fileKey);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            catch (IOException)
            {
                // ignore unreadable secret file
            }
            catch (UnauthorizedAccessException)
            {
                // ignore unreadable secret file
            }
        }

        return null;
    }
}
