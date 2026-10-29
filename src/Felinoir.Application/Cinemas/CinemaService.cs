using Felinoir.Application.Cities;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Cinemas;

public class CinemaService : ICinemaService
{
    private readonly IApplicationDbContext _db;

    public CinemaService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Cinema>> GetAllAsync(string? city = null, CancellationToken ct = default)
    {
        // City is included for the cinema's "city" field.
        IQueryable<Cinema> q = _db.Cinemas.AsNoTracking().Include(c => c.City);

        if (city is not null)
        {
            if (await _db.Cities.FindIdAsync(city, ct) is not { } cityId) return [];
            q = q.Where(c => c.CityId == cityId);
        }

        return await q.OrderBy(c => c.Name).ToListAsync(ct);
    }

    public Task<Cinema?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.Cinemas.AsNoTracking().Include(c => c.City).FirstOrDefaultAsync(c => c.Id == id, ct);
}
