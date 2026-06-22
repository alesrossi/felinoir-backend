using Felinoir.Application.Movies;

namespace Felinoir.Api.Endpoints;

public static class MovieEndpoints
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 2000;

    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/movies");

        group.MapGet("/", async (int? limit, int? offset, IMovieService movies, CancellationToken ct) =>
            Results.Ok(await movies.GetAllAsync(
                Math.Clamp(limit ?? DefaultLimit, 0, MaxLimit),
                Math.Max(offset ?? 0, 0),
                ct)));

        // Registered before "/{id}" so the literal "slug" segment is never treated as an id.
        group.MapGet("/slug/{slug}", async (string slug, IMovieService movies, CancellationToken ct) =>
        {
            var movie = await movies.GetBySlugAsync(slug, ct);
            return movie is null ? Results.NotFound(new { error = "Not found" }) : Results.Ok(movie);
        });

        group.MapGet("/{id}", async (string id, IMovieService movies, CancellationToken ct) =>
        {
            if (!int.TryParse(id, out var movieId))
                return Results.BadRequest(new { error = "Invalid id" });

            var movie = await movies.GetByIdAsync(movieId, ct);
            return movie is null ? Results.NotFound(new { error = "Not found" }) : Results.Ok(movie);
        });

        return app;
    }
}
