namespace Felinoir.Domain.Lobbies;

/// <summary>A <see cref="Lobby"/>'s identity: a time-ordered GUID (v7), so new rows append to the index.</summary>
public readonly record struct LobbyId(Guid Value)
{
    public static LobbyId New() => new(Guid.CreateVersion7());

    public static bool TryParse(string? text, out LobbyId id)
    {
        var ok = Guid.TryParse(text, out var value);
        id = new(value);
        return ok;
    }

    public override string ToString() => Value.ToString();
}

/// <summary>A <see cref="Participant"/>'s identity: a time-ordered GUID (v7).</summary>
public readonly record struct ParticipantId(Guid Value)
{
    public static ParticipantId New() => new(Guid.CreateVersion7());

    public static bool TryParse(string? text, out ParticipantId id)
    {
        var ok = Guid.TryParse(text, out var value);
        id = new(value);
        return ok;
    }

    public override string ToString() => Value.ToString();
}
