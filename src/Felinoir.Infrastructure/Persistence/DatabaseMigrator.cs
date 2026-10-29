using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// Brings a database up to the current schema. Run by the one-shot migrator before the API and
/// the scrapers start (ADR 0001); the API itself never migrates.
///
/// Databases built by the scrapers before this repository owned the schema (production, and
/// local copies of it) have the whole Baseline schema but no <c>__EFMigrationsHistory</c>. Such a
/// database is first marked as having applied Baseline, without running it; the drift test
/// proves Baseline creates exactly that schema. An empty database runs every migration.
/// </summary>
public sealed class DatabaseMigrator(ApplicationDbContext db, ILogger<DatabaseMigrator> logger)
{
    /// <summary>The migration that reproduces the schema the scrapers had built.</summary>
    public const string BaselineMigration = "20261029083355_Baseline";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await AdoptScraperBuiltDatabaseAsync(ct);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date");
            return;
        }

        logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Database schema is up to date");
    }

    private async Task AdoptScraperBuiltDatabaseAsync(CancellationToken ct)
    {
        if (await ExistsAsync("__EFMigrationsHistory", ct) || !await ExistsAsync("movies", ct)) return;

        // Lobbies were the scrapers' last schema addition before the hand-over; without them the
        // database is older than Baseline and adopting it would skip DDL it still needs.
        if (!await ExistsAsync("lobbies", ct))
            throw new InvalidOperationException(
                "The database has the scrapers' tables but not lobbies: its schema is older than the "
                + "Baseline migration and can't be adopted. Update it with the scrapers first.");

        logger.LogWarning(
            "Adopting a database built by the scrapers: recording {Migration} as applied without running it",
            BaselineMigration);

        var history = db.GetService<IHistoryRepository>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await db.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(BaselineMigration, ProductInfo.GetVersion())), ct);
        await tx.CommitAsync(ct);
    }

    private async Task<bool> ExistsAsync(string table, CancellationToken ct) =>
        await db.Database
            .SqlQuery<bool>($"SELECT to_regclass({"public.\"" + table + "\""}) IS NOT NULL AS \"Value\"")
            .SingleAsync(ct);
}
