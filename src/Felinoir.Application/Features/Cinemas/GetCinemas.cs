using Felinoir.Application.Cities;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Cinemas;

/// <summary>Cinemas by name; with <see cref="City"/> (a city id or name), only that city's.</summary>
public sealed record GetCinemasQuery(string? City = null) : IQuery<IReadOnlyList<CinemaResponse>>;

public sealed record GetCinemaByIdQuery(string Id) : IQuery<Result<CinemaResponse>>;

public sealed class GetCinemasHandler(IReadDbContext db) : IQueryHandler<GetCinemasQuery, IReadOnlyList<CinemaResponse>>
{
    public async ValueTask<IReadOnlyList<CinemaResponse>> Handle(GetCinemasQuery query, CancellationToken cancellationToken)
    {
        var cinemas = db.Cinemas;

        if (query.City is not null)
        {
            if (await db.Cities.FindIdAsync(query.City, cancellationToken) is not { } cityId) return [];
            cinemas = cinemas.Where(c => c.CityId == cityId);
        }

        return await cinemas
            .OrderBy(c => c.Name)
            .Select(CinemaResponse.Projection)
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetCinemaByIdHandler(IReadDbContext db) : IQueryHandler<GetCinemaByIdQuery, Result<CinemaResponse>>
{
    public async ValueTask<Result<CinemaResponse>> Handle(GetCinemaByIdQuery query, CancellationToken cancellationToken) =>
        await db.Cinemas
            .Where(c => c.Id == query.Id)
            .Select(CinemaResponse.Projection)
            .FirstOrDefaultAsync(cancellationToken) is { } cinema
            ? cinema
            : CatalogueErrors.NotFound;
}
