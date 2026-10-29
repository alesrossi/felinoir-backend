namespace Felinoir.Infrastructure.Persistence.ScraperTables;

/// <summary>
/// Persistence-only: a table the scrapers own the data of and this server never reads; it is
/// mapped so that EF migrations own its schema (ADR 0001).
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
