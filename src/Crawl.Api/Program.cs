using System.Diagnostics;
using Crawl.Api.Jobs;
using Crawl.Infrastructure;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;

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
        .Enrich.WithProperty("Service", "Crawl.Api")
        .WriteTo.Console());

    builder.Services.AddCrawlInfrastructure(builder.Configuration);
    builder.Services.AddScoped<JobService>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Crawl API", Version = "v1" });
    });

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
        .ConfigureResource(r => r.AddService("Crawl.Api"))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Crawl.Api");

            // Hook is always registered; export only when an endpoint is configured.
            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                t.AddOtlpExporter();
        });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CrawlDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI();

    app.Use(async (context, next) =>
    {
        if (context.Request.RouteValues.TryGetValue("jobId", out var jobIdObj)
            && jobIdObj is not null
            && Guid.TryParse(jobIdObj.ToString(), out var jobId))
        {
            using (LogContext.PushProperty("JobId", jobId))
            {
                Activity.Current?.SetTag("crawl.job_id", jobId.ToString());
                await next();
            }
        }
        else
        {
            await next();
        }
    });

    var jobs = app.MapGroup("/api/jobs").WithTags("Jobs");

    jobs.MapPost("/", async (CreateJobRequest request, JobService service, CancellationToken ct) =>
        {
            var (response, error) = await service.CreateAsync(request, ct);
            return error is null
                ? Results.Ok(response)
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["url"] = [error] });
        })
        .WithName("CreateJob")
        .WithOpenApi();

    jobs.MapGet("/{jobId:guid}", async (Guid jobId, JobService service, CancellationToken ct) =>
        {
            var summary = await service.GetSummaryAsync(jobId, ct);
            return summary is null ? Results.NotFound() : Results.Ok(summary);
        })
        .WithName("GetJob")
        .WithOpenApi();

    jobs.MapGet("/{jobId:guid}/tree", async (Guid jobId, JobService service, CancellationToken ct) =>
        {
            var exists = await service.GetSummaryAsync(jobId, ct);
            if (exists is null)
                return Results.NotFound();

            var tree = await service.GetTreeAsync(jobId, ct);
            return Results.Ok(tree);
        })
        .WithName("GetJobTree")
        .WithOpenApi();

    jobs.MapGet("/", async (int? page, int? pageSize, JobService service, CancellationToken ct) =>
        {
            var history = await service.GetHistoryAsync(page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(history);
        })
        .WithName("ListJobs")
        .WithOpenApi();

    jobs.MapPost("/{jobId:guid}/cancel", async (Guid jobId, JobService service, CancellationToken ct) =>
        {
            var (found, canceled, error) = await service.CancelAsync(jobId, ct);
            if (!found)
                return Results.NotFound();
            if (!canceled)
                return Results.Conflict(new { error });
            return Results.Ok(new { jobId, status = "Canceled" });
        })
        .WithName("CancelJob")
        .WithOpenApi();

    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Redirect("/swagger"));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Crawl.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
