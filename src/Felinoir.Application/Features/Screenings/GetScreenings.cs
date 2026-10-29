using Felinoir.Application.Cities;
using Felinoir.Application.Common;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Screenings;

/// <summary>
/// Screenings in start order, filtered by any combination of cinema, movie, city (an id or name;
/// an unknown city gives none) and an inclusive <see cref="From"/>/<see cref="To"/> range in Italian
/// time (a bound with <c>Z</c> or an offset is converted first).
/// </summary>
public sealed record GetScreeningsQuery(PageRequest Page) : IQuery<IReadOnlyList<ScreeningResponse>>
{
    public string? CinemaId { get; init; }
    public int? MovieId { get; init; }
    public string? City { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }

    /// <summary>Embed each screening's movie and cinema.</summary>
    public bool WithRelations { get; init; }
}

public sealed class GetScreeningsHandler(IReadDbContext db)
    : IQueryHandler<GetScreeningsQuery, IReadOnlyList<ScreeningResponse>>
{
    public const int DefaultLimit = 50_000;
    public const int MaxLimit = 100_000;

    public async ValueTask<IReadOnlyList<ScreeningResponse>> Handle(
        GetScreeningsQuery query, CancellationToken cancellationToken)
    {
        var screenings = db.Screenings;

        if (query.CinemaId is not null)
            screenings = screenings.Where(s => s.CinemaId == query.CinemaId);

        if (query.MovieId is not null)
            screenings = screenings.Where(s => s.MovieId == query.MovieId);

        if (query.City is not null)
        {
            if (await db.Cities.FindIdAsync(query.City, cancellationToken) is not { } cityId) return [];
            screenings = screenings.Where(s => s.Cinema!.CityId == cityId);
        }

#pragma warning disable CA1309 // Translated to a SQL text comparison; no .NET culture is involved.
        if (query.From is not null)
        {
            var from = Bound(query.From);
            screenings = screenings.Where(s => string.Compare(s.Datetime, from) >= 0);
        }

        if (query.To is not null)
        {
            var to = Bound(query.To);
            screenings = screenings.Where(s => string.Compare(s.Datetime, to) <= 0);
        }
#pragma warning restore CA1309

        var page = screenings
            .OrderBy(s => s.Datetime)
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit);

        if (!query.WithRelations)
            return await page.Select(ScreeningResponse.Projection).ToListAsync(cancellationToken);

        // Relations reuse the movie and cinema mappings in memory, rather than repeating them
        // inside one big projection.
        List<Screening> loaded = await page
            .Include(s => s.Movie)
            .Include(s => s.Cinema).ThenInclude(c => c!.City)
            .ToListAsync(cancellationToken);
        return loaded.Select(ScreeningResponse.WithRelations).ToList();
    }

    /// <summary>
    /// A bound in the stored form (Italian time, no offset), so the text comparison in SQL is a
    /// time comparison; a bound with <c>Z</c> or an offset is converted. One that doesn't parse
    /// is compared as given, as it always was.
    /// </summary>
    private static string Bound(string value) =>
        ScreeningTime.TryParseBound(value, out var time) ? time.ToString() : value;
}
