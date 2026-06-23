using Felinoir.Application.Felix;

namespace Felinoir.Api.Endpoints;

public static class FelixEndpoints
{
    public static IEndpointRouteBuilder MapFelixEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/felix/chat", async (
            FelixChatRequest? request,
            IFelixService felix,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var messages = ParseMessages(request);
            if (messages is null)
                return Results.BadRequest(new ErrorResponse("Richiesta non valida."));

            if (!felix.IsConfigured)
                return Results.Json(
                    new ErrorResponse("Felix non è configurato al momento."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            try
            {
                var filterContext = FelixFilterFormatter.Format(request!.Filters);
                var reply = await felix.GenerateAsync(messages, filterContext, ct);
                return Results.Ok(new FelixReply(reply));
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Felix").LogError(ex, "Felix generation failed");
                return Results.Json(
                    new ErrorResponse("Qualcosa è andato storto. Riprova tra qualche secondo."),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
            .WithTags("Felix")
            .WithSummary("Chat with Felix")
            .WithDescription(
                "Sends a conversation to Felix, the Gemini-backed cinema assistant for Rome. "
                + "`messages` is a non-empty array of `{ role: \"user\" | \"assistant\", content }`; optional "
                + "`filters` carry the active UI filters, appended as context to the last user message. "
                + "Returns `503` when `GEMINI_API_KEY` is not configured.")
            .Produces<FelixReply>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return app;
    }

    /// <summary>Validates the body into a clean message list, or null if it's missing/malformed.</summary>
    private static List<ChatMessage>? ParseMessages(FelixChatRequest? request)
    {
        if (request?.Messages is not { Count: > 0 } incoming) return null;

        var messages = new List<ChatMessage>(incoming.Count);
        foreach (var m in incoming)
        {
            if (m is null || (m.Role != "user" && m.Role != "assistant") || string.IsNullOrEmpty(m.Content))
                return null;

            messages.Add(new ChatMessage(m.Role, m.Content));
        }

        return messages;
    }
}
