using System.Data;
using System.Data.Common;
using Crawl.Domain.Pages;
using Microsoft.EntityFrameworkCore;

namespace Crawl.Infrastructure.Persistence;

/// <summary>
/// Idempotent page/edge writes for at-least-once delivery using PostgreSQL
/// <c>ON CONFLICT DO NOTHING</c> on <c>(JobId, Url)</c> / <c>(SourcePageId, TargetUrl)</c>.
/// </summary>
public sealed class PageUpsertStore(CrawlDbContext db)
{
    public async Task<IReadOnlyList<CrawlPage>> UpsertQueuedChildrenAsync(
        Guid jobId,
        long parentPageId,
        int depth,
        IReadOnlyList<Uri> normalizedUrls,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (normalizedUrls.Count == 0)
            return Array.Empty<CrawlPage>();

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(cancellationToken);

        var insertedIds = new List<long>();

        await using (var command = connection.CreateCommand())
        {
            var valueSql = new List<string>(normalizedUrls.Count);
            AddParam(command, "jobId", jobId);
            AddParam(command, "parentPageId", parentPageId);
            AddParam(command, "depth", depth);
            AddParam(command, "status", nameof(CrawlPageStatus.Queued));
            AddParam(command, "createdAt", createdAtUtc);

            for (var i = 0; i < normalizedUrls.Count; i++)
            {
                var name = $"url{i}";
                AddParam(command, name, normalizedUrls[i].AbsoluteUri);
                valueSql.Add($"(@jobId, @parentPageId, @{name}, @depth, @status, @createdAt)");
            }

            command.CommandText = $"""
                INSERT INTO crawl_pages ("JobId", "ParentPageId", "Url", "Depth", "Status", "CreatedAt")
                VALUES {string.Join(", ", valueSql)}
                ON CONFLICT ("JobId", "Url") DO NOTHING
                RETURNING "Id"
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                insertedIds.Add(reader.GetInt64(0));
        }

        if (insertedIds.Count == 0)
            return Array.Empty<CrawlPage>();

        return await db.Pages.AsNoTracking()
            .Where(p => insertedIds.Contains(p.Id))
            .OrderBy(p => p.Url)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceEdgesIdempotentAsync(
        long sourcePageId,
        IReadOnlyList<(string TargetUrl, bool IsInternal)> edges,
        CancellationToken cancellationToken)
    {
        await db.Edges
            .Where(e => e.SourcePageId == sourcePageId)
            .ExecuteDeleteAsync(cancellationToken);

        if (edges.Count == 0)
            return;

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        AddParam(command, "sourcePageId", sourcePageId);
        var valueSql = new List<string>(edges.Count);
        for (var i = 0; i < edges.Count; i++)
        {
            var urlName = $"url{i}";
            var intName = $"int{i}";
            AddParam(command, urlName, edges[i].TargetUrl);
            AddParam(command, intName, edges[i].IsInternal);
            valueSql.Add($"(@sourcePageId, @{urlName}, @{intName})");
        }

        command.CommandText = $"""
            INSERT INTO crawl_edges ("SourcePageId", "TargetUrl", "IsInternal")
            VALUES {string.Join(", ", valueSql)}
            ON CONFLICT ("SourcePageId", "TargetUrl") DO NOTHING
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParam(DbCommand command, string name, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        command.Parameters.Add(p);
    }
}
