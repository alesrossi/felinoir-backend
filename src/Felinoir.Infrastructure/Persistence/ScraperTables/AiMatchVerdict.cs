namespace Felinoir.Infrastructure.Persistence.ScraperTables;

/// <summary>
/// Persistence-only: mapped so that EF migrations own its schema (ADR 0001).
/// What the scraper's AI verification pass (<c>npm run enrich:ai</c>) concluded about a
/// scraped title. Keyed by (ScrapedTitle, ScrapedOriginalTitle) like
/// <see cref="TmdbLookupCache"/>, so a verdict outlives the movie row it was reached on.
/// Written by the scraper only; this server never reads or writes it.
/// </summary>
public class AiMatchVerdict
{
    public string ScrapedTitle { get; set; } = default!;
    public string ScrapedOriginalTitle { get; set; } = "";

    /// <summary>One of <c>match</c>, <c>not_a_film</c>, <c>unknown</c>.</summary>
    public string Verdict { get; set; } = default!;
    public int? TmdbId { get; set; }

    /// <summary>One of <c>high</c>, <c>low</c>.</summary>
    public string Confidence { get; set; } = default!;
    public int? CheckedTmdbId { get; set; }
    public int[] RejectedTmdbIds { get; set; } = [];
    public string? Reason { get; set; }
    public string? Model { get; set; }

    /// <summary>ISO 8601 UTC.</summary>
    public string VerifiedAt { get; set; } = default!;
}
