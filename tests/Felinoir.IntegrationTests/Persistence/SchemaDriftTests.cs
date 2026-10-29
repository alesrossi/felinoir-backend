using Felinoir.Infrastructure.Persistence;
using Felinoir.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Felinoir.IntegrationTests.Persistence;

/// <summary>
/// Proves the EF migrations own the schema (ADR 0001): they reproduce the production schema the
/// scrapers built (<c>tests/schema/production.sql</c>), adopting production through
/// <see cref="DatabaseMigrator"/> ends on exactly the schema a fresh database gets, and the model
/// has nothing left to migrate. Schemas are compared through <see cref="SchemaCatalog"/>.
///
/// When a migration changes the schema on purpose, production.sql stays as it is (it is what
/// production has before the cutover); only <see cref="Baseline_reproduces_the_production_schema"/>
/// compares against it directly.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SchemaDriftTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Baseline_reproduces_the_production_schema()
    {
        var baseline = await fixture.CreateDatabaseAsync("drift_baseline");
        await using (var db = PostgresFixture.NewDbContext(baseline))
            await db.GetService<IMigrator>().MigrateAsync(DatabaseMigrator.BaselineMigration);

        var reference = await CreateProductionCopyAsync("drift_reference");

        AssertSameSchema(await SchemaCatalog.CaptureAsync(reference), await SchemaCatalog.CaptureAsync(baseline));
    }

    [Fact]
    public async Task Adopting_production_ends_on_the_same_schema_as_a_fresh_database()
    {
        var fresh = await fixture.CreateDatabaseAsync("drift_fresh");
        await MigrateAsync(fresh);

        var adopted = await CreateProductionCopyAsync("drift_adopted");
        await MigrateAsync(adopted);

        AssertSameSchema(await SchemaCatalog.CaptureAsync(fresh), await SchemaCatalog.CaptureAsync(adopted));
        Assert.Equal(await AppliedMigrationsAsync(fresh), await AppliedMigrationsAsync(adopted));
    }

    [Fact]
    public async Task The_model_has_no_changes_without_a_migration()
    {
        await using var db = fixture.NewDbContext();
        Assert.False(db.Database.HasPendingModelChanges(),
            "The EF model differs from the latest migration: add one with `dotnet ef migrations add`.");
    }

    [Fact]
    public async Task Running_the_migrator_again_changes_nothing()
    {
        var database = await CreateProductionCopyAsync("drift_twice");
        await MigrateAsync(database);
        var schema = await SchemaCatalog.CaptureAsync(database);
        var applied = await AppliedMigrationsAsync(database);

        await MigrateAsync(database);

        AssertSameSchema(schema, await SchemaCatalog.CaptureAsync(database));
        Assert.Equal(applied, await AppliedMigrationsAsync(database));
    }

    [Fact]
    public async Task A_scraper_schema_older_than_the_baseline_is_not_adopted()
    {
        var database = await fixture.CreateDatabaseAsync("drift_old_scraper");
        await ExecuteAsync(database, "CREATE TABLE movies (id serial PRIMARY KEY, title text NOT NULL)");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrateAsync(database));

        Assert.Contains("older than the Baseline", error.Message);
        Assert.False(await HistoryExistsAsync(database));
    }

    [Fact]
    public async Task Every_migration_can_be_reverted()
    {
        var database = await fixture.CreateDatabaseAsync("drift_down");
        await MigrateAsync(database);

        await using (var db = PostgresFixture.NewDbContext(database))
            await db.GetService<IMigrator>().MigrateAsync(DatabaseMigrator.BaselineMigration);
        AssertSameSchema(
            await SchemaCatalog.CaptureAsync(await CreateProductionCopyAsync("drift_down_reference")),
            await SchemaCatalog.CaptureAsync(database));

        await using (var db = PostgresFixture.NewDbContext(database))
            await db.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
        Assert.Empty(await SchemaCatalog.CaptureAsync(database));
    }

    private static async Task MigrateAsync(string connectionString)
    {
        await using var db = PostgresFixture.NewDbContext(connectionString);
        await new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance).MigrateAsync();
    }

    /// <summary>A database with the production schema and, like production, no migration history.</summary>
    private async Task<string> CreateProductionCopyAsync(string name)
    {
        var connectionString = await fixture.CreateDatabaseAsync(name);
        var path = Path.Combine(AppContext.BaseDirectory, "schema", "production.sql");
        // psql meta-commands (\restrict) aren't SQL.
        await ExecuteAsync(connectionString,
            string.Join('\n', File.ReadAllLines(path).Where(l => !l.StartsWith('\\'))));
        return connectionString;
    }

    private static async Task<IReadOnlyList<string>> AppliedMigrationsAsync(string connectionString)
    {
        await using var db = PostgresFixture.NewDbContext(connectionString);
        return (await db.Database.GetAppliedMigrationsAsync()).ToList();
    }

    private static async Task<bool> HistoryExistsAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("""SELECT to_regclass('public."__EFMigrationsHistory"') IS NOT NULL""", conn);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AssertSameSchema(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var diff = SchemaCatalog.Diff(expected, actual);
        Assert.True(diff.Length == 0, "Schemas differ (- expected only, + actual only):\n" + diff);
    }
}
