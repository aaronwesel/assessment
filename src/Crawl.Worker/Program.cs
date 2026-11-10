using Crawl.Ai;
using Crawl.Infrastructure;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Worker.Fetching;
using Crawl.Worker.Metrics;
using Crawl.Worker.Processing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Crawl.Worker")
        .WriteTo.Console());

    builder.Services.AddCrawlInfrastructure(builder.Configuration);
    builder.Services.AddCrawlMassTransitWorker<CrawlPageRequestedConsumer>(builder.Configuration);
    builder.Services.AddCrawlAi(builder.Configuration);
    builder.Services.AddScoped<HtmlPageFetcher>();
    builder.Services.AddScoped<CrawlPageProcessor>();

    builder.Services.AddCrawlHealthChecks(builder.Configuration);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("Crawl.Worker"))
        .WithTracing(t =>
        {
            t.AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("Crawl.Worker")
                .AddSource("MassTransit");

            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                t.AddOtlpExporter();
        })
        .WithMetrics(m =>
        {
            m.AddMeter(CrawlWorkerMetrics.Meter.Name);
            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                m.AddOtlpExporter();
        });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CrawlDbContext>();
        await db.Database.MigrateAsync();
    }

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapGet("/", () => Results.Ok(new { service = "Crawl.Worker", status = "ok", bus = "MassTransit" }));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Crawl.Worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
