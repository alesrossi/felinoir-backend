using Felinoir.Application.Screenings;

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
        });

        return app;
    }
}
