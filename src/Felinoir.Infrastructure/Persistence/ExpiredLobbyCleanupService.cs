using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// Runs the database's <c>delete_expired_lobbies()</c> at startup and then hourly. The API
/// already treats expired lobbies as not found, so a missed run only delays the cleanup;
/// failures are logged and retried on the next tick rather than stopping the host.
/// </summary>
public sealed class ExpiredLobbyCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredLobbyCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var deleted = await db.Database
                    .SqlQuery<int>($"SELECT delete_expired_lobbies() AS \"Value\"")
                    .SingleAsync(stoppingToken);

                if (deleted > 0) logger.LogInformation("Deleted {Count} expired lobbies", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Expired lobby cleanup failed; retrying in {Interval}", Interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
