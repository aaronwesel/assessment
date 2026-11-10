using Crawl.Ai;
using Crawl.Infrastructure;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Crawl.Worker.Fetching;
using Crawl.Worker.Metrics;
using Crawl.Worker.Processing;
using Microsoft.EntityFrameworkCore;
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
    // Stretch: incident monitor + DeepSeek remediation agent (does not own link parsing).
    builder.Services.AddCrawlAi(builder.Configuration);
    builder.Services.AddScoped<HtmlPageFetcher>();
    builder.Services.AddScoped<CrawlPageProcessor>();
    builder.Services.AddHostedService<CrawlPageConsumer>();

    var rabbit = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
        ?? new RabbitMqOptions();
    var crawlCs = builder.Configuration.GetConnectionString("Crawl")
        ?? throw new InvalidOperationException("ConnectionStrings:Crawl is required.");

    builder.Services.AddHealthChecks()
        .AddNpgSql(crawlCs, name: "postgres")
        .AddRabbitMQ(
            $"amqp://{Uri.EscapeDataString(rabbit.UserName)}:{Uri.EscapeDataString(rabbit.Password)}@{rabbit.HostName}:{rabbit.Port}{rabbit.VirtualHost}",
            name: "rabbitmq");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("Crawl.Worker"))
        .WithTracing(t =>
        {
            t.AddHttpClientInstrumentation()
                .AddSource("Crawl.Worker");

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
        var migrateLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CrawlDbMigrator");
        await CrawlDbMigrator.MigrateSafeAsync(db, migrateLog);
    }

    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new { service = "Crawl.Worker", status = "ok" }));

    // Scale signal for local demos / operator probes (same shape as API /api/ops/queue).
    app.MapGet("/ops/queue", (RabbitMqQueueStats stats) =>
    {
        var snap = stats.ReadPageQueue();
        return Results.Ok(new
        {
            service = "Crawl.Worker",
            queue = snap.Queue,
            messagesReady = snap.MessagesReady,
            consumerCount = snap.ConsumerCount,
            dlqMessages = snap.DlqMessages,
            suggestScaleOut = snap.SuggestScaleOut(),
            suggestScaleIn = snap.SuggestScaleIn(),
            healthy = snap.Healthy,
            atUtc = snap.AtUtc
        });
    });

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
