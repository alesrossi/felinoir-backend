namespace Felinoir.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse(true)))
            .WithTags("Health")
            .WithSummary("Liveness probe")
            .WithDescription("Returns `{ \"ok\": true }` when the server is up. Does not touch the database.")
            .Produces<HealthResponse>();

        return app;
    }
}
