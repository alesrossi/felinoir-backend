using Felinoir.Application.Screenings;
using Felinoir.Domain.Entities;

namespace Felinoir.Api.Endpoints;

public static class ScreeningEndpoints
{
    private const int DefaultLimit = 50_000;
    private const int MaxLimit = 100_000;

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
            IScreeningService screenings,
            CancellationToken ct) =>
        {
            var query = new ScreeningQuery
            {
                CinemaId = cinemaId,
                MovieId = movieId,
                City = city,
                From = from,
                To = to,
                WithRelations = withRelations == "true",
                Limit = Math.Clamp(limit ?? DefaultLimit, 0, MaxLimit),
                Offset = Math.Max(offset ?? 0, 0),
            };

            return Results.Ok(await screenings.GetAsync(query, ct));
        })
            .WithTags("Screenings")
            .WithSummary("List screenings")
            .WithDescription(
                "Screenings ordered by datetime, with optional filtering. `from`/`to` are inclusive "
                + "ISO 8601 UTC bounds; set `withRelations=true` to embed the `movie` and `cinema` on each "
                + $"result. `limit` defaults to {DefaultLimit:N0} (max {MaxLimit:N0}), `offset` defaults to 0.")
            .Produces<List<Screening>>();

        return app;
    }
}
