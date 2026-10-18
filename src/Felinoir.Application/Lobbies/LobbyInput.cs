using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Felinoir.Application.Lobbies;

/// <summary>
/// Validation of user input to the lobby operations. Lengths count Unicode code points,
/// matching Postgres <c>char_length</c> in the tables' CHECK constraints.
/// </summary>
public static class LobbyInput
{
    public const int MaxNameLength = 40;
    public const int MaxNoteLength = 500;

    /// <summary>The trimmed name, or null when it is empty, too long, or contains control characters.</summary>
    public static string? NormalizeName(string? name)
    {
        if (name is null) return null;

        var trimmed = name.Trim();
        var length = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control) return null;
            length++;
        }

        return length is >= 1 and <= MaxNameLength ? trimmed : null;
    }

    public static bool IsValidNote(string note) => note.EnumerateRunes().Count() <= MaxNoteLength;

    /// <summary>SHA-256 of the device token, the only form in which a device is stored.</summary>
    public static byte[] HashDeviceToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
