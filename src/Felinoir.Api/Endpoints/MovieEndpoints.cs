using Felinoir.Api.Errors;
using Felinoir.Application.Common;
using Felinoir.Application.Features;
using Felinoir.Application.Features.Movies;
using Mediator;

namespace Felinoir.Api.Endpoints;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/movies").WithTags("Movies");

        group.MapGet("/", async (int? limit, int? offset, string? city, ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetMoviesQuery(
                    PageRequest.Create(limit, offset, GetMoviesHandler.DefaultLimit, GetMoviesHandler.MaxLimit), city), ct)))
            .WithSummary("List movies")
            .WithDescription(
                $"Movies ordered by recency. `limit` defaults to {GetMoviesHandler.DefaultLimit} (max {GetMoviesHandler.MaxLimit}); "
                + "`offset` defaults to 0. With `city` (a city id such as `rome`, or its name such as `Roma`), "
                + "only movies with a screening there.");

        // Registered before "/{id}" so the literal "slug" segment is never treated as an id.
        group.MapGet("/slug/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMovieBySlugQuery(slug), ct)).ToHttp())
            .WithSummary("Get movie by slug")
            .WithDescription("Looks up a movie by its URL slug. Registered before `/{id}` so \"slug\" is never matched as a numeric id.")
            .Produces<MovieResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", async (string id, ISender sender, CancellationToken ct) =>
                int.TryParse(id, out var movieId)
                    ? (await sender.Send(new GetMovieByIdQuery(movieId), ct)).ToHttp()
                    : CatalogueErrors.InvalidId.ToProblem())
            .WithSummary("Get movie by id")
            .WithDescription("Returns a single movie by numeric id. `400` if the id is not a number, `404` if no movie matches.")
            .Produces<MovieResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
