using System.Linq.Expressions;
using Felinoir.Domain.Entities;

namespace Felinoir.Application.Features.Movies;

/// <summary>
/// A movie as the API returns it. Same properties, in the same order, as <see cref="Movie"/>,
/// which is what clients have always received; tmdb* JSON blobs stay raw strings.
/// </summary>
public sealed record MovieResponse
{
    public required int Id { get; init; }
    public required string Title { get; init; }
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
    public string? TmdbCredits { get; init; }
    public string? TmdbEnrichedAt { get; init; }
    public string? ContentRating { get; init; }
    public string? Writer { get; init; }
    public string? Actors { get; init; }
    public string? Released { get; init; }
    public string? ImdbRating { get; init; }
    public string? ImdbVotes { get; init; }
    public string? RtRating { get; init; }
    public string? MetacriticScore { get; init; }
    public string? Awards { get; init; }
    public string? OmdbEnrichedAt { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }

    /// <summary>The mapping, as an expression EF translates into a SELECT of exactly these columns.</summary>
    public static readonly Expression<Func<Movie, MovieResponse>> Projection = m => new MovieResponse
    {
            Id = m.Id,
            Title = m.Title,
            OriginalTitle = m.OriginalTitle,
            Director = m.Director,
            DurationMinutes = m.DurationMinutes,
            Genres = m.Genres,
            Synopsis = m.Synopsis,
            PosterUrl = m.PosterUrl,
            TrailerUrl = m.TrailerUrl,
            ExternalId = m.ExternalId,
            Rating = m.Rating,
            Slug = m.Slug,
            TmdbId = m.TmdbId,
            ImdbId = m.ImdbId,
            TmdbRating = m.TmdbRating,
            TmdbVotes = m.TmdbVotes,
            TmdbTitle = m.TmdbTitle,
            TmdbTitleEn = m.TmdbTitleEn,
            TmdbOriginalTitle = m.TmdbOriginalTitle,
            TmdbPlot = m.TmdbPlot,
            TmdbPlotEn = m.TmdbPlotEn,
            TmdbGenre = m.TmdbGenre,
            TmdbGenreEn = m.TmdbGenreEn,
            TmdbLanguage = m.TmdbLanguage,
            TmdbCountry = m.TmdbCountry,
            TmdbPosterUrl = m.TmdbPosterUrl,
            TmdbTrailerUrlIt = m.TmdbTrailerUrlIt,
            TmdbTrailerUrlEn = m.TmdbTrailerUrlEn,
            TmdbPopularity = m.TmdbPopularity,
            TmdbStatus = m.TmdbStatus,
            TmdbTagline = m.TmdbTagline,
            TmdbBudget = m.TmdbBudget,
            TmdbRevenue = m.TmdbRevenue,
            TmdbHomepage = m.TmdbHomepage,
            TmdbCollection = m.TmdbCollection,
            TmdbProductionCompanies = m.TmdbProductionCompanies,
            TmdbProductionCountries = m.TmdbProductionCountries,
            TmdbSpokenLanguages = m.TmdbSpokenLanguages,
            TmdbBackdrops = m.TmdbBackdrops,
            TmdbVideos = m.TmdbVideos,
            TmdbKeywords = m.TmdbKeywords,
            TmdbRecommendations = m.TmdbRecommendations,
            TmdbWatchProviders = m.TmdbWatchProviders,
            TmdbCredits = m.TmdbCredits,
            TmdbEnrichedAt = m.TmdbEnrichedAt,
            ContentRating = m.ContentRating,
            Writer = m.Writer,
            Actors = m.Actors,
            Released = m.Released,
            ImdbRating = m.ImdbRating,
            ImdbVotes = m.ImdbVotes,
            RtRating = m.RtRating,
            MetacriticScore = m.MetacriticScore,
            Awards = m.Awards,
            OmdbEnrichedAt = m.OmdbEnrichedAt,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
    };

    private static readonly Func<Movie, MovieResponse> Map = Projection.Compile();

    /// <summary>The same mapping in memory, for a movie that is already loaded.</summary>
    public static MovieResponse From(Movie movie) => Map(movie);
}
