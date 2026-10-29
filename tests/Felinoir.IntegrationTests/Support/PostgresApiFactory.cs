using Felinoir.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Felinoir.IntegrationTests.Support;

/// <summary>
/// Boots the real API on an in-memory TestServer wired to the <see cref="PostgresFixture"/>
/// container, for endpoint tests that go all the way to the database.
///
/// <c>DATABASE_URL</c> gets the same parseable dummy as <see cref="TestApiFactory"/> (Program
/// reads it at startup); the DbContext registration is then replaced with one pointing at the
/// container. The hourly lobby cleanup is removed so it can't delete rows a test just seeded.
/// <paramref name="configureServices"/> runs last, for tests that swap further services, and
/// <paramref name="environment"/> overrides the host environment (Development by default).
/// </summary>
public sealed class PostgresApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null,
    string? environment = null)
    : WebApplicationFactory<Program>
{
    static PostgresApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "DATABASE_URL", "Host=localhost;Port=5432;Database=felinoir;Username=u;Password=p");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (environment is not null) builder.UseEnvironment(environment);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

            services.RemoveHostedService<ExpiredLobbyCleanupService>();

            configureServices?.Invoke(services);
        });
    }
}

internal static class HostedServiceCollectionExtensions
{
    /// <summary>Unregisters one hosted service, leaving the host's own (e.g. the web server) in place.</summary>
    public static void RemoveHostedService<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ImplementationType == typeof(T)).ToList())
            services.Remove(d);
    }
}
