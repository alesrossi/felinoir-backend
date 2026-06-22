using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
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
            q = q.Include(s => s.Movie).Include(s => s.Cinema);

        if (query.CinemaId is not null)
            q = q.Where(s => s.CinemaId == query.CinemaId);

        if (query.MovieId is not null)
            q = q.Where(s => s.MovieId == query.MovieId);

        if (query.City is not null)
            q = q.Where(s => s.Cinema!.City == query.City);

        if (query.From is not null)
            q = q.Where(s => string.Compare(s.Datetime, query.From) >= 0);

        if (query.To is not null)
            q = q.Where(s => string.Compare(s.Datetime, query.To) <= 0);

        return await q
            .OrderBy(s => s.Datetime)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(ct);
    }
}
