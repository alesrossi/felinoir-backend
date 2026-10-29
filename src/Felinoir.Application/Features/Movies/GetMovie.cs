using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Movies;

public sealed record GetMovieByIdQuery(int Id) : IQuery<Result<MovieResponse>>;

public sealed record GetMovieBySlugQuery(string Slug) : IQuery<Result<MovieResponse>>;

public sealed class GetMovieByIdHandler(IReadDbContext db) : IQueryHandler<GetMovieByIdQuery, Result<MovieResponse>>
{
    public async ValueTask<Result<MovieResponse>> Handle(GetMovieByIdQuery query, CancellationToken cancellationToken) =>
        await db.Movies
            .Where(m => m.Id == query.Id)
            .Select(MovieResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken) is { } movie
            ? movie
            : CatalogueErrors.NotFound;
}

public sealed class GetMovieBySlugHandler(IReadDbContext db) : IQueryHandler<GetMovieBySlugQuery, Result<MovieResponse>>
{
    public async ValueTask<Result<MovieResponse>> Handle(GetMovieBySlugQuery query, CancellationToken cancellationToken) =>
        await db.Movies
            .Where(m => m.Slug == query.Slug)
            .Select(MovieResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken) is { } movie
            ? movie
            : CatalogueErrors.NotFound;
}
