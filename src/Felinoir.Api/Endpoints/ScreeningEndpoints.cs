using Felinoir.Application.Common;
using Felinoir.Application.Features.Screenings;
using Mediator;

namespace Felinoir.Api.Endpoints;

public static class ScreeningEndpoints
{
    public static IEndpointRouteBuilder MapScreeningEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/screenings", async (
                string? cinemaId,
                int? movieId,
                string? city,
                string? from,
                string? to,
                string? withRelations,
                int? limit,
                int? offset,
                ISender sender,
                CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(
                    new GetScreeningsQuery(PageRequest.Create(
                        limit, offset, GetScreeningsHandler.DefaultLimit, GetScreeningsHandler.MaxLimit))
                    {
                        CinemaId = cinemaId,
                        MovieId = movieId,
                        City = city,
                        From = from,
                        To = to,
                        WithRelations = withRelations == "true",
                    }, ct)))
            .WithTags("Screenings")
            .WithSummary("List screenings")
            .WithDescription(
                "Screenings ordered by datetime, with optional filtering. `from`/`to` are inclusive "
                + "bounds in Italian time (a value with `Z` or an offset is converted); `city` is a city id "
                + "such as `rome` or its name such as `Roma`; set `withRelations=true` to embed the `movie` "
                + $"and `cinema` on each result. `limit` defaults to {GetScreeningsHandler.DefaultLimit:N0} "
                + $"(max {GetScreeningsHandler.MaxLimit:N0}), `offset` defaults to 0.");

        return app;
    }
}
