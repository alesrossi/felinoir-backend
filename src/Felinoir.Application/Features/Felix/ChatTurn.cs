using System.Diagnostics.CodeAnalysis;

namespace Felinoir.Application.Features.Felix;

/// <summary>Who said a turn of a Felix conversation.</summary>
public enum Speaker
{
    User,
    Assistant,
}

/// <summary>One turn of a Felix conversation.</summary>
public sealed record ChatTurn(Speaker Speaker, string Content)
{
    /// <summary>A turn from the wire: <paramref name="role"/> is exactly "user" or "assistant", and there is content.</summary>
    public static bool TryParse(string? role, string? content, [NotNullWhen(true)] out ChatTurn? turn)
    {
        turn = null;
        Speaker? speaker = role switch
        {
            "user" => Speaker.User,
            "assistant" => Speaker.Assistant,
            _ => null,
        };
        if (speaker is null || string.IsNullOrEmpty(content)) return false;

        turn = new ChatTurn(speaker.Value, content);
        return true;
    }
}
