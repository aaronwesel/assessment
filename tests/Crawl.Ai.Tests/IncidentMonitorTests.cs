using Crawl.Ai.Alerts;
using Crawl.Ai.DeepSeek;
using Crawl.Ai.Incidents;
using Crawl.Ai.Options;
using Crawl.Domain.Incidents;
using Crawl.Domain.Jobs;
using Crawl.Infrastructure.Egress;
using Crawl.Infrastructure.Http;
using Crawl.Infrastructure.Persistence;
using Crawl.Infrastructure.Remediation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Crawl.Ai.Tests;

public class IncidentMonitorTests
{
    [Fact]
    public async Task Monitor_applies_backoff_for_rate_limit_without_model()
    {
        await using var db = CreateDb();
        var politeness = new HostPolitenessGate(OptionsFactory.Create(new CrawlHttpOptions
        {
            PolitenessDelayMilliseconds = 100,
            MaxPolitenessMultiplier = 16
        }));
        var egress = new ConfigListEgressProxyProvider(
            new OptionsMonitorStub<CrawlEgressOptions>(new CrawlEgressOptions { Enabled = true, Proxies = [] }),
            NullLogger<ConfigListEgressProxyProvider>.Instance);
        var applicator = new CrawlRemediationApplicator(
            politeness,
            egress,
            OptionsFactory.Create(new CrawlHttpOptions { PolitenessDelayMilliseconds = 100 }),
            NullLogger<CrawlRemediationApplicator>.Instance);

        var monitor = new CrawlIncidentMonitor(
            db,
            new RemediationAgent(new FakeDeepSeek(false), NullLogger<RemediationAgent>.Instance),
            applicator,
            new NoopAlerts(),
            new NoopSubscriberAlerts(),
            OptionsFactory.Create(new CrawlAiOptions { Enabled = true, IncidentMonitorEnabled = true }),
            OptionsFactory.Create(new CrawlAlertOptions { EmailEnabled = false }),
            NullLogger<CrawlIncidentMonitor>.Instance);

        var fault = CrawlFaultClassifier.ClassifyHttp(Guid.NewGuid(), "corr-rl", 429, host: "example.com", retryAfterSeconds: 5);
        var incident = await monitor.HandleAsync(fault);

        Assert.Equal(RemediationKind.BackoffAndPolitenessBump, incident.RemediationKind);
        Assert.Equal("applied", incident.RemediationStatus);
        Assert.False(incident.UsedModel);
        Assert.True(politeness.GetPolitenessMultiplier("example.com") >= 2.0);
    }

    [Fact]
    public async Task Proxy_remediation_fails_closed_without_proxies()
    {
        await using var db = CreateDb();
        var politeness = new HostPolitenessGate(OptionsFactory.Create(new CrawlHttpOptions()));
        var egress = new ConfigListEgressProxyProvider(
            new OptionsMonitorStub<CrawlEgressOptions>(new CrawlEgressOptions { Enabled = true, Proxies = [] }),
            NullLogger<ConfigListEgressProxyProvider>.Instance);
        var applicator = new CrawlRemediationApplicator(
            politeness,
            egress,
            OptionsFactory.Create(new CrawlHttpOptions()),
            NullLogger<CrawlRemediationApplicator>.Instance);

        var monitor = new CrawlIncidentMonitor(
            db,
            new RemediationAgent(new FakeDeepSeek(false), NullLogger<RemediationAgent>.Instance),
            applicator,
            new NoopAlerts(),
            new NoopSubscriberAlerts(),
            OptionsFactory.Create(new CrawlAiOptions { Enabled = true, IncidentMonitorEnabled = true }),
            OptionsFactory.Create(new CrawlAlertOptions()),
            NullLogger<CrawlIncidentMonitor>.Instance);

        var fault = new CrawlFaultEvent(
            Guid.NewGuid(),
            "corr-block",
            CrawlFaultCode.HardBlock,
            CrawlFaultSeverity.Critical,
            "hard block",
            Host: "blocked.example");

        var incident = await monitor.HandleAsync(fault);
        Assert.Equal(RemediationKind.SwitchEgressProxy, incident.RemediationKind);
        Assert.Equal("fail_closed", incident.RemediationStatus);
        Assert.Contains("fail closed", incident.RemediationDetail ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Alert_template_rejects_password_leak()
    {
        var incident = CrawlIncident.Create(
            new CrawlFaultEvent(Guid.NewGuid(), "c", CrawlFaultCode.JobFailed, CrawlFaultSeverity.Error, "job failed"),
            RemediationKind.AlertOnly);

        Assert.Throws<InvalidOperationException>(() =>
            CrawlAlertTemplates.AssertNoPhi("password=hunter2"));
        var (subject, body) = CrawlAlertTemplates.RenderIncident(incident, "job_failed");
        Assert.Contains("job_failed", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    private static CrawlDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CrawlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CrawlDbContext(options);
    }

    private sealed class NoopAlerts : ICrawlAlertSender
    {
        public Task<bool> TrySendIncidentAlertAsync(
            CrawlIncident incident,
            string alertCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class NoopSubscriberAlerts : IJobSubscriberAlertService
    {
        public Task TrySendTerminalStatusAlertAsync(CrawlJob job, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TrySendIncidentAlertAsync(
            CrawlJob job,
            CrawlIncident incident,
            string alertCode,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
