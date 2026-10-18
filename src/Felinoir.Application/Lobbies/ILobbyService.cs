namespace Felinoir.Application.Lobbies;

/// <summary>
/// Group outings to a screening, shared by link. A device is identified by the token the
/// frontend sends with every call; only its SHA-256 is stored. Every change bumps the lobby's
/// version (database triggers) and returns the full lobby as seen by the calling device.
/// Lobbies expire 48 hours after their screening starts and are then reported as not found.
/// </summary>
public interface ILobbyService
{
    /// <summary>Creates a lobby for a future screening with the caller as its first participant.</summary>
    Task<LobbyResult> CreateAsync(
        int? screeningId, string? name, string? note, string deviceToken, CancellationToken ct = default);

    /// <summary>Returns <see cref="LobbyResult.Unchanged"/> when the lobby is still at <paramref name="ifVersion"/>.</summary>
    Task<LobbyResult> GetAsync(Guid id, string deviceToken, long? ifVersion = null, CancellationToken ct = default);

    Task<LobbyResult> UpdateNoteAsync(Guid id, string? note, string deviceToken, CancellationToken ct = default);

    /// <summary>Adds the caller to the lobby under <paramref name="name"/>.</summary>
    Task<LobbyResult> JoinAsync(Guid id, string? name, string deviceToken, CancellationToken ct = default);

    /// <summary>Renames any participant.</summary>
    Task<LobbyResult> RenameParticipantAsync(
        Guid id, Guid participantId, string? name, string deviceToken, CancellationToken ct = default);

    /// <summary>Removes any participant; leaving the lobby is removing yourself.</summary>
    Task<LobbyResult> RemoveParticipantAsync(
        Guid id, Guid participantId, string deviceToken, CancellationToken ct = default);

    /// <summary>
    /// Makes the participant the caller's entry: moves this device onto it and off any other
    /// entry of this device in the lobby, in one transaction.
    /// </summary>
    Task<LobbyResult> ClaimParticipantAsync(
        Guid id, Guid participantId, string deviceToken, CancellationToken ct = default);
}
