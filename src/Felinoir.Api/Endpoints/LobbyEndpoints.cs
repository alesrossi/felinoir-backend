using Felinoir.Api.Errors;
using Felinoir.Application.Features.Lobbies;
using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Felinoir.Api.Endpoints;

public static class LobbyEndpoints
{
    /// <summary>Set by the Next.js proxy on every lobby call; identifies the caller's device.</summary>
    private const string DeviceHeader = "X-Device-Token";

    private static readonly Error MissingDevice = Error.Validation("missing_device", "The X-Device-Token header is missing.");

    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        var lobbies = app.MapGroup("/lobbies")
            .WithTags("Lobbies")
            .AddEndpointFilter(RequireDeviceToken)
            // Every lobby endpoint can answer missing_device / invalid input (400) and not_found (404).
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Handlers take the device token as nullable so a missing header reaches the filter
        // (and gets the "missing_device" error) instead of failing parameter binding.

        lobbies.MapPost("/", async (
            CreateLobbyRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateLobbyCommand(request?.ScreeningId, request?.Name, request?.Note, deviceToken!), ct);
            return result.Match(
                lobby => Results.Created($"/lobbies/{lobby.Id}", lobby),
                error => error.ToProblem());
        })
            .WithSummary("Create a lobby")
            .WithDescription(
                "Creates a lobby for a screening that hasn't started yet, snapshotting its details from the "
                + "schedule, with the caller's device as the first participant `name`. `note` is optional.")
            .Produces<LobbyView>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        lobbies.MapGet("/{id}", async (
            string id,
            long? ifVersion,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!LobbyId.TryParse(id, out var lobbyId)) return LobbyErrors.NotFound.ToProblem();

            var result = await sender.Send(new GetLobbyQuery(lobbyId, deviceToken!, ifVersion), ct);
            return result.Match(
                poll => poll.NotModified ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Ok(poll.Lobby),
                error => error.ToProblem());
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
            ISender sender,
            CancellationToken ct) =>
            LobbyId.TryParse(id, out var lobbyId)
                ? (await sender.Send(new ChangeLobbyNoteCommand(lobbyId, request?.Note, deviceToken!), ct)).ToHttp()
                : LobbyErrors.NotFound.ToProblem())
            .WithSummary("Edit the lobby note")
            .WithDescription("Anyone may edit the note (at most 500 characters).")
            .Produces<LobbyView>();

        lobbies.MapPost("/{id}/participants", async (
            string id,
            LobbyParticipantNameRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
            LobbyId.TryParse(id, out var lobbyId)
                ? (await sender.Send(new JoinLobbyCommand(lobbyId, request?.Name, deviceToken!), ct)).ToHttp()
                : LobbyErrors.NotFound.ToProblem())
            .WithSummary("Join a lobby")
            .WithDescription("Adds the calling device to the lobby under `name`.")
            .Produces<LobbyView>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        lobbies.MapPatch("/{id}/participants/{pid}", async (
            string id,
            string pid,
            LobbyParticipantNameRequest? request,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
            LobbyId.TryParse(id, out var lobbyId) && ParticipantId.TryParse(pid, out var participantId)
                ? (await sender.Send(new RenameParticipantCommand(lobbyId, participantId, request?.Name, deviceToken!), ct)).ToHttp()
                : LobbyErrors.NotFound.ToProblem())
            .WithSummary("Rename a participant")
            .WithDescription("Anyone may rename any participant.")
            .Produces<LobbyView>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        lobbies.MapDelete("/{id}/participants/{pid}", async (
            string id,
            string pid,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
            LobbyId.TryParse(id, out var lobbyId) && ParticipantId.TryParse(pid, out var participantId)
                ? (await sender.Send(new RemoveParticipantCommand(lobbyId, participantId, deviceToken!), ct)).ToHttp()
                : LobbyErrors.NotFound.ToProblem())
            .WithSummary("Remove a participant")
            .WithDescription("Anyone may remove any participant; leaving is removing yourself.")
            .Produces<LobbyView>();

        lobbies.MapPost("/{id}/participants/{pid}/claim", async (
            string id,
            string pid,
            [FromHeader(Name = DeviceHeader)] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
            LobbyId.TryParse(id, out var lobbyId) && ParticipantId.TryParse(pid, out var participantId)
                ? (await sender.Send(new ClaimParticipantCommand(lobbyId, participantId, deviceToken!), ct)).ToHttp()
                : LobbyErrors.NotFound.ToProblem())
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
        if (string.IsNullOrWhiteSpace(token)) return MissingDevice.ToProblem();

        return await next(context);
    }
}
