using Felinoir.Application.Cities;
using Felinoir.Application.Common;
using Felinoir.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Movies;

/// <summary>
/// Movies, newest first (ties by id, newest first). With <see cref="City"/> (a city id or name),
/// only the movies with a screening in that city; an unknown city gives none.
/// </summary>
public sealed record GetMoviesQuery(PageRequest Page, string? City = null) : IQuery<IReadOnlyList<MovieResponse>>;

public sealed class GetMoviesHandler(IReadDbContext db) : IQueryHandler<GetMoviesQuery, IReadOnlyList<MovieResponse>>
{
    public const int DefaultLimit = 500;
    public const int MaxLimit = 2000;

    public async ValueTask<IReadOnlyList<MovieResponse>> Handle(GetMoviesQuery query, CancellationToken cancellationToken)
    {
        var movies = db.Movies;

        if (query.City is not null)
        {
            if (await db.Cities.FindIdAsync(query.City, cancellationToken) is not { } cityId) return [];
            movies = movies.Where(m => m.Screenings.Any(s => s.Cinema!.CityId == cityId));
        }

        return await movies
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip(query.Page.Offset)
            .Take(query.Page.Limit)
            .Select(MovieResponse.Projection)
            .ToListAsync(cancellationToken);
    }
}
