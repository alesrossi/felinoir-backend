using Felinoir.Api.Errors;
using Felinoir.Application.Features.Cinemas;
using Mediator;

namespace Felinoir.Api.Endpoints;

public static class CinemaEndpoints
{
    public static IEndpointRouteBuilder MapCinemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cinemas").WithTags("Cinemas");

        group.MapGet("/", async (string? city, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetCinemasQuery(city), ct)))
            .WithSummary("List cinemas")
            .WithDescription(
                "Returns all cinemas, ordered by name. With `city` (a city id such as `rome`, or its name "
                + "such as `Roma`), only the cinemas listed under that city.");

        group.MapGet("/{id}", async (string id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCinemaByIdQuery(id), ct)).ToHttp())
            .WithSummary("Get cinema by id")
            .WithDescription("Returns a single cinema by its string id (slug, e.g. `cinema-farnese`), or `404`.")
            .Produces<CinemaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
