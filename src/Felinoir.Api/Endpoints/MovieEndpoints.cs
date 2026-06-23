using Felinoir.Application.Movies;
using Felinoir.Domain.Entities;

namespace Felinoir.Api.Endpoints;

public static class MovieEndpoints
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 2000;

    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/movies").WithTags("Movies");

        group.MapGet("/", async (int? limit, int? offset, IMovieService movies, CancellationToken ct) =>
            Results.Ok(await movies.GetAllAsync(
                Math.Clamp(limit ?? DefaultLimit, 0, MaxLimit),
                Math.Max(offset ?? 0, 0),
                ct)))
            .WithSummary("List movies")
            .WithDescription($"Movies ordered by recency. `limit` defaults to {DefaultLimit} (max {MaxLimit}); `offset` defaults to 0.")
            .Produces<List<Movie>>();

        // Registered before "/{id}" so the literal "slug" segment is never treated as an id.
        group.MapGet("/slug/{slug}", async (string slug, IMovieService movies, CancellationToken ct) =>
        {
            var movie = await movies.GetBySlugAsync(slug, ct);
            return movie is null ? Results.NotFound(new ErrorResponse("Not found")) : Results.Ok(movie);
        })
            .WithSummary("Get movie by slug")
            .WithDescription("Looks up a movie by its URL slug. Registered before `/{id}` so \"slug\" is never matched as a numeric id.")
            .Produces<Movie>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", async (string id, IMovieService movies, CancellationToken ct) =>
        {
            if (!int.TryParse(id, out var movieId))
                return Results.BadRequest(new ErrorResponse("Invalid id"));

            var movie = await movies.GetByIdAsync(movieId, ct);
            return movie is null ? Results.NotFound(new ErrorResponse("Not found")) : Results.Ok(movie);
        })
            .WithSummary("Get movie by id")
            .WithDescription("Returns a single movie by numeric id. `400` if the id is not a number, `404` if no movie matches.")
            .Produces<Movie>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}
