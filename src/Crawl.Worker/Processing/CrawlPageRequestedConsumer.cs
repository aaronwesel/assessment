using Crawl.Ai.Incidents;
using Crawl.Domain.Fetching;
using Crawl.Domain.Incidents;
using Crawl.Domain.Messaging;
using Crawl.Infrastructure.Observability;
using Crawl.Worker.Metrics;
using MassTransit;

namespace Crawl.Worker.Processing;

/// <summary>
/// MassTransit consumer for <see cref="CrawlPageRequested"/>. Retries via
/// <c>UseMessageRetry(r => r.Exponential(...))</c>; exhausted/poison land on crawl.page.dlq.
/// </summary>
public sealed class CrawlPageRequestedConsumer(
    CrawlPageProcessor processor,
    ICrawlIncidentMonitor incidentMonitor,
    ILogger<CrawlPageRequestedConsumer> logger) : IConsumer<CrawlPageRequested>
{
    public async Task Consume(ConsumeContext<CrawlPageRequested> context)
    {
        var message = context.Message;
        var correlation =
            context.Headers.Get<string>(CorrelationId.HeaderName)
            ?? context.CorrelationId?.ToString("N")
            ?? $"{message.JobId:N}:{message.PageId}";

        CorrelationId.Current = correlation;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlation))
        using (Serilog.Context.LogContext.PushProperty("JobId", message.JobId))
        using (Serilog.Context.LogContext.PushProperty("PageId", message.PageId))
        {
            try
            {
                await processor.ProcessAsync(message, context.CancellationToken);
            }
            catch (Exception ex) when (ex is TransientCrawlException
                                           or HttpRequestException
                                           or TaskCanceledException
                                           or TimeoutException
                                           or IOException)
            {
                CrawlWorkerMetrics.TransientRetries.Add(1);
                await TryReportFaultAsync(message, ex, correlation, exhausted: false, context.CancellationToken);
                // Rethrow so MassTransit UseMessageRetry applies exponential backoff.
                throw;
            }
            catch (Exception ex)
            {
                CrawlWorkerMetrics.PoisonMessages.Add(1);
                await TryReportFaultAsync(message, ex, correlation, exhausted: true, context.CancellationToken);
                logger.LogError(
                    ex,
                    "Poison/non-transient failure JobId={JobId} PageId={PageId} CorrelationId={CorrelationId}",
                    message.JobId,
                    message.PageId,
                    correlation);
                throw;
            }
        }
    }

    private async Task TryReportFaultAsync(
        CrawlPageRequested message,
        Exception ex,
        string correlationId,
        bool exhausted,
        CancellationToken cancellationToken)
    {
        try
        {
            CrawlFaultEvent fault;
            if (ex is TransientCrawlException transient)
            {
                var code = exhausted
                    ? CrawlFaultCode.RepeatedDlq
                    : transient.FaultCode
                      ?? CrawlFaultClassifier.ClassifyException(
                          message.JobId, correlationId, ex, transient.Host, message.PageId, attempt: 1).Code;
                var severity = CrawlFaultClassifier.ClassifySeverity(code, 1, transient.RetryAfterSeconds);
                fault = new CrawlFaultEvent(
                    message.JobId,
                    correlationId,
                    code,
                    severity,
                    CrawlFaultClassifier.SanitizeSummary(transient.Message),
                    transient.Host,
                    message.PageId,
                    transient.HttpStatus,
                    transient.RetryAfterSeconds,
                    Attempt: 1);
            }
            else
            {
                fault = exhausted
                    ? new CrawlFaultEvent(
                        message.JobId,
                        correlationId,
                        CrawlFaultCode.RepeatedDlq,
                        CrawlFaultSeverity.Error,
                        CrawlFaultClassifier.SanitizeSummary($"DLQ candidate: {ex.GetType().Name}"),
                        PageId: message.PageId,
                        Attempt: 1)
                    : CrawlFaultClassifier.ClassifyException(
                        message.JobId, correlationId, ex, pageId: message.PageId, attempt: 1);
            }

            await incidentMonitor.HandleAsync(fault, cancellationToken);
        }
        catch (Exception reportEx)
        {
            logger.LogWarning(reportEx, "Failed to report crawl incident for JobId={JobId}", message.JobId);
        }
    }
}
