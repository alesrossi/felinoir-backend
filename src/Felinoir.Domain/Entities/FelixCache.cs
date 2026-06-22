namespace Felinoir.Domain.Entities;

/// <summary>
/// Single-row table (Id always = 1) that persists the active Gemini context cache
/// reference across server restarts.
/// </summary>
public class FelixCache
{
    public int Id { get; set; } = 1;

    /// <summary>Gemini resource name, e.g. "cachedContents/abc123".</summary>
    public string CacheName { get; set; } = default!;

    /// <summary>ISO 8601 UTC.</summary>
    public string ExpiresAt { get; set; } = default!;
    public string CreatedAt { get; set; } = default!;
}
