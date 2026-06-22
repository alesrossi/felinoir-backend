using Felinoir.Application.Cinemas;

namespace Felinoir.Api.Endpoints;

public static class CinemaEndpoints
{
    public static IEndpointRouteBuilder MapCinemaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cinemas");

        group.MapGet("/", async (ICinemaService cinemas, CancellationToken ct) =>
            Results.Ok(await cinemas.GetAllAsync(ct)));

        group.MapGet("/{id}", async (string id, ICinemaService cinemas, CancellationToken ct) =>
        {
            var cinema = await cinemas.GetByIdAsync(id, ct);
            return cinema is null ? Results.NotFound(new { error = "Not found" }) : Results.Ok(cinema);
        });

        return app;
    }
}
