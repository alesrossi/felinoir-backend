namespace Felinoir.Domain.Entities;

/// <summary>
/// A film: scraped core fields plus TMDB and OMDb enrichment columns. Written by the scrapers;
/// this server only reads it, so it is init-only.
/// JSON blob fields (tmdb*) are stored and returned as raw JSON strings, never deserialized.
/// All timestamps are ISO 8601 UTC stored as TEXT.
/// </summary>
public class Movie
{
    public int Id { get; init; }
    public string Title { get; init; } = default!;
    public string? OriginalTitle { get; init; }
    public string? Director { get; init; }
    public int? DurationMinutes { get; init; }
    public string? Genres { get; init; }
    public string? Synopsis { get; init; }
    public string? PosterUrl { get; init; }
    public string? TrailerUrl { get; init; }
    public string? ExternalId { get; init; }
    public string? Rating { get; init; }
    public string? Slug { get; init; }

    // TMDB enrichment
    public int? TmdbId { get; init; }
    public string? ImdbId { get; init; }
    public string? TmdbRating { get; init; }
    public string? TmdbVotes { get; init; }
    public string? TmdbTitle { get; init; }
    public string? TmdbTitleEn { get; init; }
    public string? TmdbOriginalTitle { get; init; }
    public string? TmdbPlot { get; init; }
    public string? TmdbPlotEn { get; init; }
    public string? TmdbGenre { get; init; }
    public string? TmdbGenreEn { get; init; }
    public string? TmdbLanguage { get; init; }
    public string? TmdbCountry { get; init; }
    public string? TmdbPosterUrl { get; init; }
    public string? TmdbTrailerUrlIt { get; init; }
    public string? TmdbTrailerUrlEn { get; init; }
    public string? TmdbPopularity { get; init; }
    public string? TmdbStatus { get; init; }
    public string? TmdbTagline { get; init; }
    public long? TmdbBudget { get; init; }
    public long? TmdbRevenue { get; init; }
    public string? TmdbHomepage { get; init; }
    public string? TmdbCollection { get; init; }
    public string? TmdbProductionCompanies { get; init; }
    public string? TmdbProductionCountries { get; init; }
    public string? TmdbSpokenLanguages { get; init; }
    public string? TmdbBackdrops { get; init; }
    public string? TmdbVideos { get; init; }
    public string? TmdbKeywords { get; init; }
    public string? TmdbRecommendations { get; init; }
    public string? TmdbWatchProviders { get; init; }

    /// <summary>
    /// Trimmed TMDB credits as raw JSON: <c>{ directors, writers, cast, composers, cinematographers }</c>,
    /// each person <c>{ id, name, profile_path }</c> (writers add <c>jobs</c>, cast adds <c>character</c>).
    /// Written by the scraper's TMDB enrichment.
    /// </summary>
    public string? TmdbCredits { get; init; }
    public string? TmdbEnrichedAt { get; init; }

    public string? ContentRating { get; init; }
    public string? Writer { get; init; }
    public string? Actors { get; init; }
    public string? Released { get; init; }

    // OMDb enrichment
    public string? ImdbRating { get; init; }
    public string? ImdbVotes { get; init; }
    public string? RtRating { get; init; }
    public string? MetacriticScore { get; init; }

    /// <summary>OMDb's awards line as given, e.g. "Won 2 Oscars. 126 wins &amp; 375 nominations total".</summary>
    public string? Awards { get; init; }
    public string? OmdbEnrichedAt { get; init; }

    public string CreatedAt { get; init; } = default!;
    public string UpdatedAt { get; init; } = default!;

    public ICollection<Screening> Screenings { get; init; } = new List<Screening>();
}
