namespace Felinoir.Application.Lobbies;

/// <summary>Business failures of the lobby operations; the API maps each to a status and error code.</summary>
public enum LobbyError
{
    /// <summary>Lobby doesn't exist or has expired, or the participant isn't in it.</summary>
    NotFound,
    InvalidName,
    InvalidNote,
    /// <summary>Another participant of the lobby already has this name (case-insensitive).</summary>
    NameTaken,
    /// <summary>This device already has an entry in the lobby.</summary>
    AlreadyJoined,
    ScreeningNotFound,
    /// <summary>The screening has already started, so no new lobby can be created for it.</summary>
    ScreeningPast,
}

/// <summary>Outcome of a lobby operation: the updated lobby, an error, or "not modified" (polling).</summary>
public sealed record LobbyResult
{
    public LobbyView? Lobby { get; private init; }
    public LobbyError? Error { get; private init; }
    public bool NotModified { get; private init; }

    public static LobbyResult Ok(LobbyView lobby) => new() { Lobby = lobby };
    public static LobbyResult Fail(LobbyError error) => new() { Error = error };
    public static LobbyResult Unchanged { get; } = new() { NotModified = true };

    public static implicit operator LobbyResult(LobbyError error) => Fail(error);
}
