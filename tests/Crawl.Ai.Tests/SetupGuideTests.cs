using System.Net;
using System.Net.Http;
using Crawl.Ai.SetupGuide;
namespace Crawl.Ai.Tests;

public class SetupGuideTests
{
    [Fact]
    public async Task Preview_heuristic_question_never_exposes_env_var_names()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><body>Welcome</body></html>")
        });
        var svc = new SetupGuideService(factory, new FakeDeepSeek(false));
        var resp = await svc.PreviewAsync(new SetupGuideRequest(
            "https://books.toscrape.com",
            2,
            1,
            "what depth should I use?"));

        Assert.False(resp.UsedModel);
        Assert.NotNull(resp.ModelAnswer);
        Assert.NotNull(resp.DegradedReason);
        Assert.DoesNotContain("DEEPSEEK", resp.DegradedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offline", resp.DegradedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_uses_model_when_configured()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>ok</html>")
        });
        var svc = new SetupGuideService(factory, new FakeDeepSeek(true, "Use depth 2 for demos."));
        var resp = await svc.PreviewAsync(new SetupGuideRequest(
            "https://example.com",
            2,
            0,
            "depth advice?"));

        Assert.True(resp.UsedModel);
        Assert.Equal("Use depth 2 for demos.", resp.ModelAnswer);
        Assert.Null(resp.DegradedReason);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHandler(_handler));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) =>
                Task.FromResult(_handler(request));
        }
    }
}
