using Felinoir.Application.Cities;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Screenings;

public class ScreeningService : IScreeningService
{
    private readonly IApplicationDbContext _db;

    public ScreeningService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Screening>> GetAsync(ScreeningQuery query, CancellationToken ct = default)
    {
        IQueryable<Screening> q = _db.Screenings.AsNoTracking();

        if (query.WithRelations)
            q = q.Include(s => s.Movie).Include(s => s.Cinema).ThenInclude(c => c!.City);

        if (query.CinemaId is not null)
            q = q.Where(s => s.CinemaId == query.CinemaId);

        if (query.MovieId is not null)
            q = q.Where(s => s.MovieId == query.MovieId);

        if (query.City is not null)
        {
            if (await _db.Cities.FindIdAsync(query.City, ct) is not { } cityId) return [];
            q = q.Where(s => s.Cinema!.CityId == cityId);
        }

#pragma warning disable CA1309 // Translated to a SQL text comparison; no .NET culture is involved.
        if (query.From is not null)
        {
            var from = Bound(query.From);
            q = q.Where(s => string.Compare(s.Datetime, from) >= 0);
        }

        if (query.To is not null)
        {
            var to = Bound(query.To);
            q = q.Where(s => string.Compare(s.Datetime, to) <= 0);
        }
#pragma warning restore CA1309

        return await q
            .OrderBy(s => s.Datetime)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// A bound in the stored form (Italian time, no offset), so the text comparison in SQL is a
    /// time comparison; a bound with <c>Z</c> or an offset is converted. One that doesn't parse
    /// is compared as given, as it always was.
    /// </summary>
    private static string Bound(string value) =>
        ScreeningTime.TryParseBound(value, out var time) ? time.ToString() : value;
}
