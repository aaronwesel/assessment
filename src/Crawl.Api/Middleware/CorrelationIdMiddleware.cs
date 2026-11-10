using System.Diagnostics;
using Crawl.Infrastructure.Observability;
using Serilog.Context;

namespace Crawl.Api.Middleware;

/// <summary>
/// Injects/propagates <c>X-Correlation-Id</c>, pushes Serilog context, tags the current Activity.
/// MassTransit publisher reads <see cref="CorrelationId.Current"/> into message headers.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? Guid.NewGuid().ToString("N")
            : incoming.Trim();

        CorrelationId.Current = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
