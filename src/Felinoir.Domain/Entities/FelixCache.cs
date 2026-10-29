namespace Felinoir.Domain.Entities;

/// <summary>
/// The active Gemini context cache of one city's Felix corpus, persisted so it survives server
/// restarts. One row per <see cref="City"/>.
/// </summary>
public class FelixCache
{
    public string CityId { get; set; } = default!;

    /// <summary>Gemini resource name, e.g. "cachedContents/abc123".</summary>
    public string CacheName { get; set; } = default!;

    /// <summary>ISO 8601 UTC.</summary>
    public string ExpiresAt { get; set; } = default!;
    public string CreatedAt { get; set; } = default!;
}
