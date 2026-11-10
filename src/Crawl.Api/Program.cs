using System.Diagnostics;
using Crawl.Ai;
using Crawl.Api.Jobs;
using Crawl.Api.Middleware;
using Crawl.Infrastructure;
using Crawl.Infrastructure.Messaging;
using Crawl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
    builder.Services.AddCrawlMassTransitPublisher(builder.Configuration);
    builder.Services.AddCrawlAi(builder.Configuration);
    builder.Services.AddScoped<JobService>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Crawl API", Version = "v1" });
    });

    builder.Services.AddCrawlHealthChecks(builder.Configuration);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("Crawl.Api"))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("Crawl.Api")
                .AddSource("MassTransit");

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

    app.UseMiddleware<CorrelationIdMiddleware>();
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
            var tree = await service.GetTreeAsync(jobId, ct);
            return tree is null ? Results.NotFound() : Results.Ok(tree);
        })
        .WithName("GetJobTree")
        .WithOpenApi();

    jobs.MapGet("/{jobId:guid}/pages", async (Guid jobId, JobService service, CancellationToken ct) =>
        {
            var pages = await service.GetPagesAsync(jobId, ct);
            return pages is null ? Results.NotFound() : Results.Ok(pages);
        })
        .WithName("ListJobPages")
        .WithOpenApi();

    jobs.MapGet("/{jobId:guid}/pages/{pageId:long}/links", async (Guid jobId, long pageId, JobService service, CancellationToken ct) =>
        {
            var links = await service.GetPageLinksAsync(jobId, pageId, ct);
            return links is null ? Results.NotFound() : Results.Ok(links);
        })
        .WithName("GetPageLinks")
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
            return Results.NoContent();
        })
        .WithName("CancelJob")
        .WithOpenApi();

    jobs.MapPut("/{jobId:guid}/alert-email", async (
            Guid jobId,
            SetJobAlertEmailRequest request,
            JobService service,
            CancellationToken ct) =>
        {
            var (found, summary, error) = await service.SetAlertEmailAsync(jobId, request, ct);
            if (!found)
                return Results.NotFound();
            if (error is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = [error] });
            return Results.Ok(summary);
        })
        .WithName("SetJobAlertEmail")
        .WithOpenApi();

    app.MapCrawlAi();

    // Liveness: process up. Readiness: Postgres + RabbitMQ. Keep /health as ready for Compose.
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
