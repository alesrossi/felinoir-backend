using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;
using FluentValidation;
using Mediator;

namespace Felinoir.Application.Features.Lobbies;

// The commands on an existing lobby. Each is a thin call into the Lobby aggregate, which owns the
// rules; validators only check the request's shape.

/// <summary>Edits the note (anyone may).</summary>
public sealed record ChangeLobbyNoteCommand(LobbyId LobbyId, string? Note, string DeviceToken)
    : ICommand<Result<LobbyView>>;

/// <summary>Adds the calling device to the lobby under a name.</summary>
public sealed record JoinLobbyCommand(LobbyId LobbyId, string? Name, string DeviceToken)
    : ICommand<Result<LobbyView>>;

/// <summary>Renames any participant.</summary>
public sealed record RenameParticipantCommand(
    LobbyId LobbyId, ParticipantId ParticipantId, string? Name, string DeviceToken) : ICommand<Result<LobbyView>>;

/// <summary>Removes any participant; leaving is removing yourself.</summary>
public sealed record RemoveParticipantCommand(LobbyId LobbyId, ParticipantId ParticipantId, string DeviceToken)
    : ICommand<Result<LobbyView>>;

/// <summary>Makes a participant the calling device's entry, releasing the device's other one.</summary>
public sealed record ClaimParticipantCommand(LobbyId LobbyId, ParticipantId ParticipantId, string DeviceToken)
    : ICommand<Result<LobbyView>>;

public sealed class ChangeLobbyNoteValidator : AbstractValidator<ChangeLobbyNoteCommand>
{
    public ChangeLobbyNoteValidator()
    {
        RuleFor(c => c.DeviceToken).IsDeviceToken();
        RuleFor(c => c.Note).IsLobbyNote();
    }
}

public sealed class JoinLobbyValidator : AbstractValidator<JoinLobbyCommand>
{
    public JoinLobbyValidator()
    {
        RuleFor(c => c.DeviceToken).IsDeviceToken();
        RuleFor(c => c.Name).IsParticipantName();
    }
}

public sealed class RenameParticipantValidator : AbstractValidator<RenameParticipantCommand>
{
    public RenameParticipantValidator()
    {
        RuleFor(c => c.DeviceToken).IsDeviceToken();
        RuleFor(c => c.Name).IsParticipantName();
    }
}

public sealed class RemoveParticipantValidator : AbstractValidator<RemoveParticipantCommand>
{
    public RemoveParticipantValidator() => RuleFor(c => c.DeviceToken).IsDeviceToken();
}

public sealed class ClaimParticipantValidator : AbstractValidator<ClaimParticipantCommand>
{
    public ClaimParticipantValidator() => RuleFor(c => c.DeviceToken).IsDeviceToken();
}

public sealed class ChangeLobbyNoteHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
    : LobbyCommandHandler(lobbies, views, time), ICommandHandler<ChangeLobbyNoteCommand, Result<LobbyView>>
{
    public ValueTask<Result<LobbyView>> Handle(ChangeLobbyNoteCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.LobbyId, command.DeviceToken, (lobby, _) =>
        {
            LobbyNote.TryCreate(command.Note, out var note);
            return lobby.ChangeNote(note);
        }, cancellationToken);
}

public sealed class JoinLobbyHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
    : LobbyCommandHandler(lobbies, views, time), ICommandHandler<JoinLobbyCommand, Result<LobbyView>>
{
    private readonly TimeProvider _time = time;

    public ValueTask<Result<LobbyView>> Handle(JoinLobbyCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.LobbyId, command.DeviceToken, (lobby, device) =>
        {
            ParticipantName.TryCreate(command.Name, out var name);
            return lobby.Join(name, device, _time.GetUtcNow());
        }, cancellationToken);
}

public sealed class RenameParticipantHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
    : LobbyCommandHandler(lobbies, views, time), ICommandHandler<RenameParticipantCommand, Result<LobbyView>>
{
    public ValueTask<Result<LobbyView>> Handle(RenameParticipantCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.LobbyId, command.DeviceToken, (lobby, _) =>
        {
            ParticipantName.TryCreate(command.Name, out var name);
            return lobby.Rename(command.ParticipantId, name);
        }, cancellationToken);
}

public sealed class RemoveParticipantHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
    : LobbyCommandHandler(lobbies, views, time), ICommandHandler<RemoveParticipantCommand, Result<LobbyView>>
{
    public ValueTask<Result<LobbyView>> Handle(RemoveParticipantCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.LobbyId, command.DeviceToken,
            (lobby, _) => lobby.Remove(command.ParticipantId), cancellationToken);
}

public sealed class ClaimParticipantHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
    : LobbyCommandHandler(lobbies, views, time), ICommandHandler<ClaimParticipantCommand, Result<LobbyView>>
{
    public ValueTask<Result<LobbyView>> Handle(ClaimParticipantCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.LobbyId, command.DeviceToken,
            (lobby, device) => lobby.Claim(command.ParticipantId, device), cancellationToken);
}
