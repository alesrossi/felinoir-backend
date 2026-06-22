using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Cinemas;

public class CinemaService : ICinemaService
{
    private readonly IApplicationDbContext _db;

    public CinemaService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Cinema>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Cinema?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.Cinemas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
