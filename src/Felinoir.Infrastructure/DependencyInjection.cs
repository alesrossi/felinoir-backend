using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Features.Lobbies;
using Felinoir.Application.Features.Felix;
using Felinoir.Infrastructure.Configuration;
using Felinoir.Infrastructure.Felix;
using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Felinoir.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);

        // One context per request behind every persistence abstraction.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ILobbyRepository, LobbyRepository>();

        // No pg_cron on the server, so expired lobbies are deleted from here.
        services.AddHostedService<ExpiredLobbyCleanupService>();

        services.AddScoped<IFelixContextStore, FelixContextStore>();

        // A typed client: the factory pools and recycles handlers (so DNS changes are picked up),
        // and the standard resilience pipeline adds timeouts, retries with jitter on transient
        // failures (5xx, 408, 429) and a circuit breaker. Transient, so never held by a singleton.
        services.AddHttpClient<IGeminiClient, GeminiClient>()
            .AddStandardResilienceHandler(options =>
            {
                // Generating up to 700 tokens, or uploading the corpus, can take a while.
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(75);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.Retry.MaxRetryAttempts = 2;
            });

        return services;
    }

    /// <summary>
    /// Only the database: the <see cref="ApplicationDbContext"/> on <c>DATABASE_URL</c> and the
    /// <see cref="DatabaseMigrator"/>. Enough for the migrator, which needs nothing else.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseUrl.ToNpgsqlConnectionString(configuration["DATABASE_URL"]);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<DatabaseMigrator>();

        return services;
    }
}
