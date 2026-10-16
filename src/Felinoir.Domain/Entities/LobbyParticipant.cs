namespace Felinoir.Domain.Entities;

/// <summary>
/// A name in a <see cref="Lobby"/>. Names are unique per lobby case-insensitively
/// (<c>lobby_participants_name_uniq</c>), and a device can hold at most one name per lobby
/// (<c>lobby_participants_one_per_device</c>).
/// </summary>
public class LobbyParticipant
{
    public Guid Id { get; set; }
    public Guid LobbyId { get; set; }

    /// <summary>1 to 40 characters (CHECK).</summary>
    public string Name { get; set; } = default!;

    /// <summary>SHA-256 of the device token; null when no device has claimed this name.</summary>
    public byte[]? DeviceHash { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public Lobby? Lobby { get; set; }
}
