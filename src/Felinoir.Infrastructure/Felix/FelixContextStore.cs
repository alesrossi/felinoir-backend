using System.Globalization;
using Felinoir.Application.Common;
using Felinoir.Application.Features.Felix;
using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Felinoir.Infrastructure.Felix;

/// <summary>
/// <see cref="IFelixContextStore"/> on <c>felix_cache</c>: one row per city with the Gemini cache's
/// name, its expiry and (as <c>created_at</c>) when its corpus was built.
/// </summary>
public sealed partial class FelixContextStore(ApplicationDbContext db, ILogger<FelixContextStore> logger)
    : IFelixContextStore
{
    public async Task<FelixContext?> LoadAsync(string cityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await db.FelixCache.AsNoTracking().FirstOrDefaultAsync(f => f.CityId == cityId, cancellationToken);
            if (row is null || !TryParseUtc(row.ExpiresAt, out var expiresAt) || !TryParseUtc(row.CreatedAt, out var builtAt))
                return null;

            return FelixContext.Cached(row.CacheName, expiresAt, builtAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogLoadFailed(logger, ex, cityId);
            return null;
        }
    }

    public async Task SaveAsync(string cityId, FelixContext context, CancellationToken cancellationToken = default)
    {
        if (!context.IsCached) return;

        try
        {
            var expiresAt = UtcText.Format(context.CacheExpiresAt!.Value);
            var builtAt = UtcText.Format(context.BuiltAt);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO felix_cache (city_id, cache_name, expires_at, created_at)
                VALUES ({cityId}, {context.CacheName}, {expiresAt}, {builtAt})
                ON CONFLICT (city_id) DO UPDATE
                   SET cache_name = EXCLUDED.cache_name,
                       expires_at = EXCLUDED.expires_at,
                       created_at = EXCLUDED.created_at
                """, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFailed(logger, ex, cityId);
        }
    }

    private static bool TryParseUtc(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the persisted Felix context of {City}")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, string city);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not persist the Felix context of {City}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string city);
}
