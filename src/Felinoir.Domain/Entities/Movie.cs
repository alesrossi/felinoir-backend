namespace Felinoir.Domain.Entities;

/// <summary>
/// A film. Scraped core fields plus TMDB and OMDb enrichment columns.
/// JSON blob fields (tmdb*) are stored and returned as raw JSON strings, never deserialized.
/// All timestamps are ISO 8601 UTC stored as TEXT.
/// </summary>
public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? OriginalTitle { get; set; }
    public string? Director { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Genres { get; set; }
    public string? Synopsis { get; set; }
    public string? PosterUrl { get; set; }
    public string? TrailerUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? Rating { get; set; }
    public string? Slug { get; set; }

    // TMDB enrichment
    public int? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? TmdbRating { get; set; }
    public string? TmdbVotes { get; set; }
    public string? TmdbTitle { get; set; }
    public string? TmdbTitleEn { get; set; }
    public string? TmdbOriginalTitle { get; set; }
    public string? TmdbPlot { get; set; }
    public string? TmdbPlotEn { get; set; }
    public string? TmdbGenre { get; set; }
    public string? TmdbGenreEn { get; set; }
    public string? TmdbLanguage { get; set; }
    public string? TmdbCountry { get; set; }
    public string? TmdbPosterUrl { get; set; }
    public string? TmdbTrailerUrlIt { get; set; }
    public string? TmdbTrailerUrlEn { get; set; }
    public string? TmdbPopularity { get; set; }
    public string? TmdbStatus { get; set; }
    public string? TmdbTagline { get; set; }
    public int? TmdbBudget { get; set; }
    public int? TmdbRevenue { get; set; }
    public string? TmdbHomepage { get; set; }
    public string? TmdbCollection { get; set; }
    public string? TmdbProductionCompanies { get; set; }
    public string? TmdbProductionCountries { get; set; }
    public string? TmdbSpokenLanguages { get; set; }
    public string? TmdbBackdrops { get; set; }
    public string? TmdbVideos { get; set; }
    public string? TmdbKeywords { get; set; }
    public string? TmdbRecommendations { get; set; }
    public string? TmdbWatchProviders { get; set; }
    public string? TmdbEnrichedAt { get; set; }

    public string? ContentRating { get; set; }
    public string? Writer { get; set; }
    public string? Actors { get; set; }
    public string? Released { get; set; }

    // OMDb enrichment
    public string? ImdbRating { get; set; }
    public string? ImdbVotes { get; set; }
    public string? RtRating { get; set; }
    public string? MetacriticScore { get; set; }
    public string? OmdbEnrichedAt { get; set; }

    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;

    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
