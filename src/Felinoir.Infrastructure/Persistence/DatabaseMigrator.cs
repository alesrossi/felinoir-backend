using System.Reflection;
using Felinoir.Infrastructure.Persistence.Migrations;
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
public sealed partial class DatabaseMigrator(ApplicationDbContext db, ILogger<DatabaseMigrator> logger)
{
    /// <summary>
    /// Id of the migration that reproduces the schema the scrapers had built, read from the
    /// migration's own <see cref="MigrationAttribute"/> so the two can't disagree.
    /// </summary>
    public static readonly string BaselineMigration =
        typeof(Baseline).GetCustomAttribute<MigrationAttribute>()!.Id;

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await AdoptScraperBuiltDatabaseAsync(ct);

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            LogUpToDate(logger);
            return;
        }

        LogApplying(logger, pending.Count, pending);
        await db.Database.MigrateAsync(ct);
        LogUpToDate(logger);
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

        LogAdopting(logger, BaselineMigration);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date")]
    private static partial void LogUpToDate(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} migration(s): {Migrations}")]
    private static partial void LogApplying(ILogger logger, int count, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Adopting a database built by the scrapers: recording {Migration} as applied without running it")]
    private static partial void LogAdopting(ILogger logger, string migration);
}
