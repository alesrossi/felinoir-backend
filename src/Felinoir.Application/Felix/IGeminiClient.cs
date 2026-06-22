namespace Felinoir.Application.Felix;

/// <summary>
/// Low-level Gemini REST operations used by Felix. Implementations own the HTTP details,
/// the model/endpoint, and the system prompt; orchestration (caching, fallback) lives in
/// <see cref="IFelixService"/>.
/// </summary>
public interface IGeminiClient
{
    /// <summary>Whether <c>GEMINI_API_KEY</c> is configured. When false, Felix is unavailable.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Uploads the corpus as a Gemini context cache (24 h TTL). Returns the cache reference,
    /// or null if caching is unavailable (e.g. corpus below the minimum token threshold).
    /// </summary>
    Task<GeminiCacheRef?> CreateCacheAsync(string corpus, CancellationToken ct = default);

    /// <summary>
    /// Generates a reply using an existing context cache. Returns null when the cache has
    /// expired (HTTP 404) so the caller can fall back to inline context; throws on other errors.
    /// </summary>
    Task<string?> GenerateWithCacheAsync(
        IReadOnlyList<ChatMessage> messages, string cacheName, CancellationToken ct = default);

    /// <summary>Generates a reply with the corpus passed inline as context on this request.</summary>
    Task<string> GenerateInlineAsync(
        IReadOnlyList<ChatMessage> messages, string corpus, CancellationToken ct = default);
}

/// <summary>A Gemini context-cache reference. <see cref="Name"/> looks like "cachedContents/abc123".</summary>
public sealed record GeminiCacheRef(string Name, DateTimeOffset ExpiresAt);
