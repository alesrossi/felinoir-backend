using System.Globalization;
using System.Text;

namespace Felinoir.Domain.Lobbies;

/// <summary>
/// A <see cref="Participant"/>'s name: trimmed, 1 to <see cref="MaxLength"/> characters, no control
/// characters. Lengths count Unicode code points, as Postgres <c>char_length</c> does in the table's
/// CHECK. Names are compared ignoring case (<see cref="SameAs"/>).
/// </summary>
public readonly record struct ParticipantName
{
    public const int MaxLength = 40;

    private ParticipantName(string value) => Value = value;

    public string Value { get; }

    /// <summary>The trimmed name, or false when the text can't be a name.</summary>
    public static bool TryCreate(string? text, out ParticipantName name)
    {
        name = default;
        if (text is null) return false;

        var trimmed = text.Trim();
        var length = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control) return false;
            length++;
        }

        if (length is < 1 or > MaxLength) return false;
        name = new ParticipantName(trimmed);
        return true;
    }

    public static bool IsValid(string? text) => TryCreate(text, out _);

    /// <summary>A name that was valid when stored; for reading it back from the database.</summary>
    public static ParticipantName FromStored(string value) => new(value);

    public bool SameAs(ParticipantName other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Value;
}
