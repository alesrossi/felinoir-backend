using Felinoir.Api.Errors;
using Felinoir.Application.Features.Felix;
using Mediator;

namespace Felinoir.Api.Endpoints;

public static class FelixEndpoints
{
    public static IEndpointRouteBuilder MapFelixEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/felix/chat", async (string? city, FelixChatRequest? request, ISender sender, CancellationToken ct) =>
            {
                var messages = request?.Messages?.Select(m => m is null ? null : new FelixMessageInput(m.Role, m.Content)).ToList();
                return (await sender.Send(new AskFelixCommand(city, messages, request?.Filters), ct)).ToHttp();
            })
            .WithTags("Felix")
            .WithSummary("Chat with Felix")
            .WithDescription(
                "Sends a conversation to Felix, the Gemini-backed cinema assistant. "
                + "`messages` is a non-empty array of `{ role: \"user\" | \"assistant\", content }`; optional "
                + "`filters` carry the active UI filters, appended as context to the last user message. "
                + "`city` (query; a city id such as `rome` or its name) picks the city Felix answers about, "
                + "Rome by default. Returns `503` when `GEMINI_API_KEY` is not configured.")
            .Produces<FelixReply>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
