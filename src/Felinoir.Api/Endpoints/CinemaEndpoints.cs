using Felinoir.Application.Cinemas;
using Felinoir.Domain.Entities;

namespace Felinoir.Api.Endpoints;

public static class CinemaEndpoints
{
    public static IEndpointRouteBuilder MapCinemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cinemas").WithTags("Cinemas");

        group.MapGet("/", async (ICinemaService cinemas, CancellationToken ct) =>
            Results.Ok(await cinemas.GetAllAsync(ct)))
            .WithSummary("List cinemas")
            .WithDescription("Returns all cinemas, ordered by name.")
            .Produces<List<Cinema>>();

        group.MapGet("/{id}", async (string id, ICinemaService cinemas, CancellationToken ct) =>
        {
            var cinema = await cinemas.GetByIdAsync(id, ct);
            return cinema is null ? Results.NotFound(new ErrorResponse("Not found")) : Results.Ok(cinema);
        })
            .WithSummary("Get cinema by id")
            .WithDescription("Returns a single cinema by its string id (slug, e.g. `cinema-farnese`), or `404`.")
            .Produces<Cinema>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}
