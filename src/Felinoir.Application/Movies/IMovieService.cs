using Felinoir.Domain.Entities;

namespace Felinoir.Application.Movies;

public interface IMovieService
{
    /// <summary>Movies ordered by recency. Caller is responsible for clamping limit/offset.</summary>
    Task<IReadOnlyList<Movie>> GetAllAsync(int limit, int offset, CancellationToken ct = default);

    Task<Movie?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<Movie?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
