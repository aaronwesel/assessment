using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Crawl.Infrastructure.Persistence;

/// <summary>
/// Applies EF migrations under a Postgres advisory lock so API + N workers
/// can start concurrently without racing on <c>__EFMigrationsHistory</c>.
/// </summary>
public static class CrawlDbMigrator
{
    // Arbitrary stable lock keys for "crawl-ef-migrate".
    private const int LockClass = 872015;
    private const int LockId = 1;

    public static async Task MigrateSafeAsync(
        CrawlDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using (var lockCmd = connection.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_advisory_lock(@c, @k)";
            var p1 = lockCmd.CreateParameter();
            p1.ParameterName = "c";
            p1.Value = LockClass;
            lockCmd.Parameters.Add(p1);
            var p2 = lockCmd.CreateParameter();
            p2.ParameterName = "k";
            p2.Value = LockId;
            lockCmd.Parameters.Add(p2);
            await lockCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await db.Database.MigrateAsync(cancellationToken);
                    return;
                }
                catch (PostgresException ex) when ((ex.SqlState is "42P07" or "23505") && attempt < maxAttempts)
                {
                    // Concurrent create of history table / unique race — brief backoff and retry.
                    logger.LogWarning(
                        ex,
                        "Migration race (attempt {Attempt}/{Max}); retrying",
                        attempt,
                        maxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsTransientMigration(ex))
                {
                    logger.LogWarning(
                        ex,
                        "Transient migration failure (attempt {Attempt}/{Max}); retrying",
                        attempt,
                        maxAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
                }
            }
        }
        finally
        {
            try
            {
                await using var unlockCmd = connection.CreateCommand();
                unlockCmd.CommandText = "SELECT pg_advisory_unlock(@c, @k)";
                var p1 = unlockCmd.CreateParameter();
                p1.ParameterName = "c";
                p1.Value = LockClass;
                unlockCmd.Parameters.Add(p1);
                var p2 = unlockCmd.CreateParameter();
                p2.ParameterName = "k";
                p2.Value = LockId;
                unlockCmd.Parameters.Add(p2);
                await unlockCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to release migration advisory lock");
            }
        }
    }

    private static bool IsTransientMigration(Exception ex) =>
        ex is PostgresException { SqlState: "40P01" or "40001" or "55P03" }
        || ex.InnerException is PostgresException { SqlState: "42P07" or "23505" or "40P01" or "40001" };
}
