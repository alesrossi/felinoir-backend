using Felinoir.Domain.Common;

namespace Felinoir.Domain.Lobbies;

/// <summary>The Lobby's errors; their codes are what clients switch on in <c>error</c>.</summary>
public static class LobbyErrors
{
    /// <summary>No such lobby, an expired one, or no such participant in it.</summary>
    public static readonly Error NotFound = Error.NotFound("not_found");

    public static readonly Error InvalidName = Error.Validation("invalid_name");
    public static readonly Error InvalidNote = Error.Validation("invalid_note");

    /// <summary>Another participant already has this name (ignoring case).</summary>
    public static readonly Error NameTaken = Error.Conflict("name_taken");

    /// <summary>This device already has an entry in the lobby.</summary>
    public static readonly Error AlreadyJoined = Error.Conflict("already_joined");

    public static readonly Error ScreeningNotFound = Error.Unprocessable("screening_not_found");

    /// <summary>The screening has started, so a lobby can no longer be created for it.</summary>
    public static readonly Error ScreeningPast = Error.Unprocessable("screening_past");
}
