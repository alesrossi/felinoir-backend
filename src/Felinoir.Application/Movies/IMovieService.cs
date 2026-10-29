using Felinoir.Domain.Entities;

namespace Felinoir.Application.Movies;

public interface IMovieService
{
    /// <summary>
    /// Movies ordered by recency; with <paramref name="city"/>, only those with a screening in that
    /// city. Caller is responsible for clamping limit/offset.
    /// </summary>
    Task<IReadOnlyList<Movie>> GetAllAsync(int limit, int offset, string? city = null, CancellationToken ct = default);

    Task<Movie?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Movie?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
