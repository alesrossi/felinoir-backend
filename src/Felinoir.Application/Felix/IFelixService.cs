namespace Felinoir.Application.Felix;

/// <summary>
/// Orchestrates a Felix chat turn: ensures a Gemini context cache (or inline corpus) is
/// ready, appends any active UI filter context to the last user message, and generates a reply.
/// </summary>
public interface IFelixService
{
    /// <summary>True when the underlying Gemini client is configured (<c>GEMINI_API_KEY</c> set).</summary>
    bool IsConfigured { get; }

    /// <param name="messages">Conversation so far; the last entry is the new user message.</param>
    /// <param name="filterContext">Optional plain-text block describing active UI filters.</param>
    Task<string> GenerateAsync(
        IReadOnlyList<ChatMessage> messages, string? filterContext = null, CancellationToken ct = default);
}
