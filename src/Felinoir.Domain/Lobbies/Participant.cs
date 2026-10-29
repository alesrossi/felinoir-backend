using Felinoir.Domain.Common;

namespace Felinoir.Domain.Lobbies;

/// <summary>
/// A named entry in a <see cref="Lobby"/>, claimed by one <see cref="DeviceHash"/> or unclaimed
/// (see CONTEXT.md). Part of the Lobby aggregate: only the lobby changes it.
/// </summary>
public sealed class Participant : Entity<ParticipantId>
{
    internal Participant(ParticipantId id, LobbyId lobbyId, ParticipantName name, DeviceHash? device, DateTimeOffset joinedAt)
        : base(id)
    {
        LobbyId = lobbyId;
        Name = name;
        Device = device;
        JoinedAt = joinedAt;
    }

    private Participant() : base(default) { } // EF

    public LobbyId LobbyId { get; private init; }
    public ParticipantName Name { get; internal set; }

    /// <summary>The device holding this entry, or null when it is unclaimed.</summary>
    public DeviceHash? Device { get; internal set; }

    public DateTimeOffset JoinedAt { get; private init; }

    public bool IsHeldBy(DeviceHash device) => Device == device;
}
