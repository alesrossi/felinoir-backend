using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Felix;
using Felinoir.Infrastructure.Configuration;
using Felinoir.Infrastructure.Felix;
using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Felinoir.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // No pg_cron on the server, so expired lobbies are deleted from here.
        services.AddHostedService<ExpiredLobbyCleanupService>();

        var cinemasPath = configuration["CINEMAS_CONFIG_PATH"];
        cinemasPath = string.IsNullOrWhiteSpace(cinemasPath) ? CinemasConfigLocator.Locate() : cinemasPath;
        services.AddSingleton<ICinemaConfigProvider>(_ => new CinemasYamlConfigProvider(cinemasPath));

        // One long-lived HttpClient for Gemini, kept private to the client.
        services.AddSingleton<IGeminiClient>(sp => new GeminiClient(
            new HttpClient(),
            configuration,
            sp.GetRequiredService<ILogger<GeminiClient>>()));

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
