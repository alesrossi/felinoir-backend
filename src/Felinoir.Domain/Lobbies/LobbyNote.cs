namespace Felinoir.Domain.Lobbies;

/// <summary>A <see cref="Lobby"/>'s free-text note: at most <see cref="MaxLength"/> characters (code points), possibly empty.</summary>
public readonly record struct LobbyNote
{
    public const int MaxLength = 500;

    public static readonly LobbyNote Empty = new("");

    private LobbyNote(string value) => Value = value;

    public string Value => field ?? "";

    public static bool TryCreate(string? text, out LobbyNote note)
    {
        note = default;
        if (text is null || text.EnumerateRunes().Count() > MaxLength) return false;
        note = new LobbyNote(text);
        return true;
    }

    public static bool IsValid(string? text) => TryCreate(text, out _);

    /// <summary>A note that was valid when stored; for reading it back from the database.</summary>
    public static LobbyNote FromStored(string value) => new(value);

    public override string ToString() => Value;
}
