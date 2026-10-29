using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Felix;

/// <summary>Loads the <see cref="CorpusSnapshot"/> of one city, as of now.</summary>
public sealed class CorpusSnapshotLoader(IReadDbContext db, TimeProvider time)
{
    private const int MovieLimit = 1000;
    private const int ScreeningLimit = 100_000;

    public async Task<CorpusSnapshot> LoadAsync(string cityId, CancellationToken cancellationToken = default)
    {
        // Screening times are Italian wall clock, so "now" must be too for the text comparison.
        var now = ScreeningTime.FromInstant(time.GetUtcNow());
        var nowText = now.ToString();

        var cityName = await db.Cities
            .Where(c => c.Id == cityId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? cityId;

        var movies = await db.Movies
            .OrderBy(m => m.Id)
            .Take(MovieLimit)
            .ToListAsync(cancellationToken);

#pragma warning disable CA1309 // Translated to a SQL text comparison; no .NET culture is involved.
        var screenings = await db.Screenings
            .Where(s => s.Cinema!.CityId == cityId && string.Compare(s.Datetime, nowText) >= 0)
#pragma warning restore CA1309
            .Take(ScreeningLimit)
            .ToListAsync(cancellationToken);

        var cinemas = await db.Cinemas.Where(c => c.CityId == cityId).ToListAsync(cancellationToken);

        return new CorpusSnapshot(cityName, now, movies, cinemas, screenings);
    }
}
