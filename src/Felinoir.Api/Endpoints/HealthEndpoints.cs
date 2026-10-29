using Felinoir.Application.Features.Health;
using Mediator;

namespace Felinoir.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (ISender sender, CancellationToken ct) =>
                TypedResults.Ok(await sender.Send(new GetHealthQuery(), ct)))
            .WithTags("Health")
            .WithSummary("Liveness probe")
            .WithDescription("Returns `{ \"ok\": true }` when the server is up. Does not touch the database.");

        return app;
    }
}
