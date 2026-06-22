using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Movies;

public class MovieService : IMovieService
{
    private readonly IApplicationDbContext _db;

    public MovieService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<Movie>> GetAllAsync(int limit, int offset, CancellationToken ct = default) =>
        await _db.Movies
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

    public Task<Movie?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Movie?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Slug == slug, ct);
}
