using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crawl.Ai.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.DeepSeek;

/// <summary>
/// Thin DeepSeek chat client. Graceful when DEEPSEEK_API_KEY / options key is absent.
/// </summary>
public sealed class DeepSeekClient : IDeepSeekClient
{
    private readonly HttpClient _http;
    private readonly CrawlAiOptions _options;
    private readonly ILogger<DeepSeekClient> _logger;

    public DeepSeekClient(HttpClient http, IOptions<CrawlAiOptions> options, ILogger<DeepSeekClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    public async Task<DeepSeekResult> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new DeepSeekResult(null, false, AiDegradedCopy.OfflineHeuristics);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Content = JsonContent.Create(new ChatRequest(
                _options.DeepSeekModel,
                new[]
                {
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", userPrompt)
                },
                Temperature: 0.2));

            using var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("DeepSeek HTTP {Status}; degrading without model output", (int)res.StatusCode);
                return new DeepSeekResult(null, false, AiDegradedCopy.HttpFallback((int)res.StatusCode));
            }

            await using var stream = await res.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var parsed = await JsonSerializer.DeserializeAsync<ChatResponse>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var content = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return new DeepSeekResult(null, false, AiDegradedCopy.EmptyContent);
            }

            return new DeepSeekResult(content, true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "DeepSeek call failed; degrading");
            return new DeepSeekResult(null, false, AiDegradedCopy.Unavailable);
        }
    }

    private string? ResolveApiKey() => DeepSeekApiKeyResolver.Resolve(_options.DeepSeekApiKey);

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessageDto? Message { get; set; }
    }

    private sealed class ChatMessageDto
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
