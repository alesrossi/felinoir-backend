using Felinoir.Application.Lobbies;
using Microsoft.AspNetCore.Mvc;

namespace Felinoir.Api.Endpoints;

public static class LobbyEndpoints
{
    /// <summary>Set by the Next.js proxy on every lobby call; identifies the caller's device.</summary>
    private const string DeviceHeader = "X-Device-Token";

    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        var lobbies = app.MapGroup("/lobbies")
            .WithTags("Lobbies")
            .AddEndpointFilter(RequireDeviceToken)
            // Every lobby endpoint can answer missing_device / invalid input (400) and not_found (404).
            .WithMetadata(
                new ProducesResponseTypeAttribute(typeof(ErrorResponse), StatusCodes.Status400BadRequest),
                new ProducesResponseTypeAttribute(typeof(ErrorResponse), StatusCodes.Status404NotFound));

        // Handlers take the device token as nullable so a missing header reaches the filter
        // (and gets the JSON "missing_device" error) instead of failing parameter binding.

        lobbies.MapPost("/", async (
            CreateLobbyRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(
                request?.ScreeningId, request?.Name, request?.Note, deviceToken!, ct);
            return result.Lobby is { } lobby
                ? Results.Created($"/lobbies/{lobby.Id}", lobby)
                : ToHttp(result);
        })
            .WithSummary("Create a lobby")
            .WithDescription(
                "Creates a lobby for a screening that hasn't started yet, snapshotting its details from the "
                + "schedule, with the caller's device as the first participant `name`. `note` is optional.")
            .Produces<LobbyView>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        lobbies.MapGet("/{id}", async (
            string id,
            long? ifVersion,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId)) return NotFound();
            var result = await service.GetAsync(lobbyId, deviceToken!, ifVersion, ct);
            return result.NotModified ? Results.StatusCode(StatusCodes.Status304NotModified) : ToHttp(result);
        })
            .WithSummary("Get a lobby")
            .WithDescription(
                "The lobby as seen by the calling device. With `ifVersion`, returns `304` and no body when "
                + "the lobby is still at that version (for polling).")
            .Produces<LobbyView>()
            .Produces(StatusCodes.Status304NotModified);

        lobbies.MapPatch("/{id}", async (
            string id,
            UpdateLobbyNoteRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId)) return NotFound();
            return ToHttp(await service.UpdateNoteAsync(lobbyId, request?.Note, deviceToken!, ct));
        })
            .WithSummary("Edit the lobby note")
            .WithDescription("Anyone may edit the note (at most 500 characters).")
            .Produces<LobbyView>();

        lobbies.MapPost("/{id}/participants", async (
            string id,
            LobbyParticipantNameRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId)) return NotFound();
            return ToHttp(await service.JoinAsync(lobbyId, request?.Name, deviceToken!, ct));
        })
            .WithSummary("Join a lobby")
            .WithDescription("Adds the calling device to the lobby under `name`.")
            .Produces<LobbyView>()
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        lobbies.MapPatch("/{id}/participants/{pid}", async (
            string id,
            string pid,
            LobbyParticipantNameRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId) || !Guid.TryParse(pid, out var participantId))
                return NotFound();
            return ToHttp(await service.RenameParticipantAsync(lobbyId, participantId, request?.Name, deviceToken!, ct));
        })
            .WithSummary("Rename a participant")
            .WithDescription("Anyone may rename any participant.")
            .Produces<LobbyView>()
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        lobbies.MapDelete("/{id}/participants/{pid}", async (
            string id,
            string pid,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId) || !Guid.TryParse(pid, out var participantId))
                return NotFound();
            return ToHttp(await service.RemoveParticipantAsync(lobbyId, participantId, deviceToken!, ct));
        })
            .WithSummary("Remove a participant")
            .WithDescription("Anyone may remove any participant; leaving is removing yourself.")
            .Produces<LobbyView>();

        lobbies.MapPost("/{id}/participants/{pid}/claim", async (
            string id,
            string pid,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ILobbyService service,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(id, out var lobbyId) || !Guid.TryParse(pid, out var participantId))
                return NotFound();
            return ToHttp(await service.ClaimParticipantAsync(lobbyId, participantId, deviceToken!, ct));
        })
            .WithSummary("Claim a participant")
            .WithDescription(
                "Makes the participant the calling device's entry, moving the device off any other entry "
                + "it had in the lobby.")
            .Produces<LobbyView>();

        return app;
    }

    private static async ValueTask<object?> RequireDeviceToken(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var token = context.HttpContext.Request.Headers[DeviceHeader].ToString();
        if (string.IsNullOrWhiteSpace(token))
            return Results.BadRequest(new ErrorResponse("missing_device"));

        return await next(context);
    }

    private static IResult NotFound() => Results.NotFound(new ErrorResponse("not_found"));

    private static IResult ToHttp(LobbyResult result) => result switch
    {
        { Lobby: { } lobby } => Results.Ok(lobby),
        { Error: LobbyError.NotFound } => NotFound(),
        { Error: LobbyError.InvalidName } => Results.BadRequest(new ErrorResponse("invalid_name")),
        { Error: LobbyError.InvalidNote } => Results.BadRequest(new ErrorResponse("invalid_note")),
        { Error: LobbyError.NameTaken } => Results.Conflict(new ErrorResponse("name_taken")),
        { Error: LobbyError.AlreadyJoined } => Results.Conflict(new ErrorResponse("already_joined")),
        { Error: LobbyError.ScreeningNotFound } => Results.UnprocessableEntity(new ErrorResponse("screening_not_found")),
        { Error: LobbyError.ScreeningPast } => Results.UnprocessableEntity(new ErrorResponse("screening_past")),
        _ => throw new InvalidOperationException($"Unhandled lobby result: {result}"),
    };
}
