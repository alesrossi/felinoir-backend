using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felinoir.IntegrationTests.Support;

/// <summary>
/// Boots a single throwaway Postgres container for the whole test run and applies
/// the EF migrations, giving every service test the real schema and real query
/// translation (snake_case, lexicographic TEXT comparisons).
///
/// On this host Calico CNI blocks Docker's bridge network, so — like the project's
/// docker-compose — the container runs with <c>network_mode: host</c> and Postgres
/// binds directly to a free host port. That also means the usual mapped-port plumbing
/// doesn't apply: we use the generic container builder (the PostgreSql module injects a
/// mapped-port wait strategy that never fires under host networking), set the port via
/// <c>PGPORT</c>, build the connection string by hand, and poll the connection ourselves.
///
/// Shared across test classes via <see cref="PostgresCollection"/>; the container is
/// fully isolated and disposable, so it never touches the real <c>cinema</c> database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Db = "felinoir_test";
    private const string User = "test";
    private const string Password = "test";

    private readonly int _port = GetFreeTcpPort();
    private string _connectionString = null!;
    private IContainer _container = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _connectionString =
            $"Host=127.0.0.1;Port={_port};Database={Db};Username={User};Password={Password}";

        _container = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_DB", Db)
            .WithEnvironment("POSTGRES_USER", User)
            .WithEnvironment("POSTGRES_PASSWORD", Password)
            .WithEnvironment("PGPORT", _port.ToString())
            // Run on the host network to bypass the Calico-blocked bridge.
            .WithCreateParameterModifier(p => p.HostConfig.NetworkMode = "host")
            // The generic builder's readiness is just "container is running"; real DB
            // readiness is handled by WaitForPostgresAsync, since a port-based probe
            // can't work under host networking.
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new ContainerIsRunning()))
            .Build();

        await _container.StartAsync();
        await WaitForPostgresAsync();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString)
            // Mirror the production DI wiring so the model maps to the snake_case
            // schema the migration builds.
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>Polls a real connection until Postgres accepts it (or we give up).</summary>
    private async Task WaitForPostgresAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                return;
            }
            catch (NpgsqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(250);
            }
        }
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A new context per call — mirrors the scoped lifetime services get in the app.</summary>
    public ApplicationDbContext NewDbContext() => new(_options);

    /// <summary>Clears all data so each test starts from an empty database.</summary>
    public async Task ResetAsync()
    {
        await using var db = NewDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE screenings, movies, cinemas, felix_cache, tmdb_lookup_cache RESTART IDENTITY CASCADE;");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Pass-through wait strategy: succeeds as soon as Testcontainers has started the
    /// container. Real DB readiness is handled by <see cref="WaitForPostgresAsync"/>,
    /// because the built-in strategies rely on a mapped port we don't have under host networking.
    /// </summary>
    private sealed class ContainerIsRunning : IWaitUntil
    {
        public Task<bool> UntilAsync(IContainer container) =>
            Task.FromResult(container.State == TestcontainersStates.Running);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// Base class for data-layer tests: resets the database before each test and
/// exposes the shared fixture. Derive and decorate with <c>[Collection(PostgresCollection.Name)]</c>.
/// </summary>
public abstract class PostgresTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; }

    protected PostgresTestBase(PostgresFixture fixture) => Fixture = fixture;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Persists the given entities via a dedicated context, then returns.</summary>
    protected async Task SeedAsync(params object[] entities)
    {
        await using var db = Fixture.NewDbContext();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }
}
