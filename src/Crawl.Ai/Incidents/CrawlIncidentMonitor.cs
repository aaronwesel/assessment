using Crawl.Ai.Alerts;
using Crawl.Ai.Options;
using Crawl.Domain.Incidents;
using Crawl.Infrastructure.Persistence;
using Crawl.Infrastructure.Remediation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crawl.Ai.Incidents;

public interface ICrawlIncidentMonitor
{
    Task<CrawlIncident> HandleAsync(CrawlFaultEvent fault, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrawlIncident>> ListForJobAsync(Guid jobId, int take = 20, CancellationToken cancellationToken = default);

    Task<CrawlIncident?> GetLatestForJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Classifies (already done by caller or re-checked), asks DeepSeek remediation agent, applies safe fixes, persists, optional email.
/// Stretch observability — does not replace deterministic crawl parsing / RabbitMQ pipeline.
/// </summary>
public sealed class CrawlIncidentMonitor(
    CrawlDbContext db,
    IRemediationAgent agent,
    ICrawlRemediationApplicator applicator,
    ICrawlAlertSender alerts,
    IOptions<CrawlAiOptions> aiOptions,
    IOptions<CrawlAlertOptions> alertOptions,
    ILogger<CrawlIncidentMonitor> logger) : ICrawlIncidentMonitor
{
    public async Task<CrawlIncident> HandleAsync(CrawlFaultEvent fault, CancellationToken cancellationToken = default)
    {
        if (!aiOptions.Value.Enabled || !aiOptions.Value.IncidentMonitorEnabled)
        {
            var disabled = CrawlIncident.Create(fault, RemediationKind.None);
            disabled.ApplyRemediation("disabled", "Incident monitor feature flag off.", false, null, false);
            db.Incidents.Add(disabled);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return disabled;
        }

        var advice = await agent.AdviseAsync(fault, cancellationToken).ConfigureAwait(false);
        var incident = CrawlIncident.Create(fault, advice.Kind);

        RemediationApplyResult apply;
        try
        {
            apply = await applicator.ApplyAsync(fault, advice.Kind, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Remediation apply failed for {CorrelationId}", fault.CorrelationId);
            apply = new RemediationApplyResult(false, "error", "Remediation applicator threw; see logs.");
        }

        var alertCode = ResolveAlertCode(fault.Code, advice.Kind, apply);
        var shouldAlert = ShouldAlert(fault, advice.Kind, apply);
        var alertSent = false;
        if (shouldAlert && alertOptions.Value.EmailEnabled)
        {
            // Persist first so alert template can reference remediation status.
            incident.ApplyRemediation(apply.Status, apply.Detail, advice.UsedModel, advice.Rationale, alertSent: false);
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            alertSent = await alerts.TrySendIncidentAlertAsync(incident, alertCode, cancellationToken)
                .ConfigureAwait(false);
            incident.ApplyRemediation(apply.Status, apply.Detail, advice.UsedModel, advice.Rationale, alertSent);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return incident;
        }

        incident.ApplyRemediation(apply.Status, apply.Detail, advice.UsedModel, advice.Rationale, alertSent);
        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Crawl incident JobId={JobId} CorrelationId={CorrelationId} Fault={Fault} Remediation={Kind}/{Status}",
            fault.JobId,
            fault.CorrelationId,
            fault.Code,
            advice.Kind,
            apply.Status);

        return incident;
    }

    public async Task<IReadOnlyList<CrawlIncident>> ListForJobAsync(
        Guid jobId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        return await db.Incidents.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<CrawlIncident?> GetLatestForJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        db.Incidents.AsNoTracking()
            .Where(i => i.JobId == jobId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool ShouldAlert(CrawlFaultEvent fault, RemediationKind kind, RemediationApplyResult apply)
    {
        if (fault.Code is CrawlFaultCode.JobFailed or CrawlFaultCode.RepeatedDlq)
            return true;
        if (kind == RemediationKind.SwitchEgressProxy)
            return true;
        if (apply.Status is "fail_closed")
            return true;
        if (fault.Severity >= CrawlFaultSeverity.Error)
            return true;
        return false;
    }

    private static string ResolveAlertCode(CrawlFaultCode code, RemediationKind kind, RemediationApplyResult apply)
    {
        if (code == CrawlFaultCode.JobFailed) return "job_failed";
        if (code == CrawlFaultCode.RepeatedDlq) return "repeated_dlq";
        if (kind == RemediationKind.SwitchEgressProxy && apply.Status == "fail_closed")
            return "block_remediation_fail_closed";
        if (kind == RemediationKind.SwitchEgressProxy) return "block_remediation";
        return "crawl_incident";
    }
}
