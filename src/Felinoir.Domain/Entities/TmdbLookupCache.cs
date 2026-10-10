namespace Felinoir.Domain.Entities;

/// <summary>
/// Caches scraped-title → TMDB id resolutions to avoid redundant API calls during enrichment.
/// Keyed by (ScrapedTitle, ScrapedOriginalTitle).
/// </summary>
public class TmdbLookupCache
{
    public string ScrapedTitle { get; set; } = default!;
    public string ScrapedOriginalTitle { get; set; } = "";
    public int TmdbId { get; set; }
    public string LookedUpAt { get; set; } = default!;

    /// <summary>Poster of the scraped row this entry was written for (kept for the scraper's AI verification pass).</summary>
    public string? ScrapedPosterUrl { get; set; }
}
