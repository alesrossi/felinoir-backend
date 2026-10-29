namespace Felinoir.Domain.Entities;

/// <summary>
/// The active Gemini context cache of one city's Felix corpus, persisted so it survives server
/// restarts. One row per <see cref="City"/>; <see cref="CreatedAt"/> is when the corpus was built.
/// Written with an upsert by the Felix context store, so read-only here.
/// </summary>
public class FelixCache
{
    public string CityId { get; init; } = default!;

    /// <summary>Gemini resource name, e.g. "cachedContents/abc123".</summary>
    public string CacheName { get; init; } = default!;

    /// <summary>ISO 8601 UTC.</summary>
    public string ExpiresAt { get; init; } = default!;
    public string CreatedAt { get; init; } = default!;
}
