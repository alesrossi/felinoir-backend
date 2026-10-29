using Felinoir.Application.Felix;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;

namespace Felinoir.UnitTests.Felix;

/// <summary>
/// The corpus text from plain data: no database. Which screenings are upcoming and which cinemas
/// belong to the city is the loader's job (see the integration tests); here every screening given
/// is an upcoming one.
/// </summary>
public class CorpusRendererTests
{
    // Screening times are Italian wall clock, as the scrapers store them.
    private const string Future = "2026-06-24T21:00:00";

    /// <summary>The movies, cinemas and screenings a test renders.</summary>
    private sealed class Data
    {
        public List<Movie> Movies { get; } = [];
        public List<Cinema> Cinemas { get; } = [];
        public List<Screening> Screenings { get; } = [];
    }

    private static Data NewDb() => new();

    private static Task<string> BuildAsync(Data db)
    {
        Assert.True(ScreeningTime.TryParse("2026-06-22T20:30:00", out var now));
        return Task.FromResult(CorpusRenderer.Render(new CorpusSnapshot(
            "Roma", now, db.Movies.OrderBy(m => m.Id).ToList(), db.Cinemas, db.Screenings)));
    }

    [Fact]
    public async Task Includes_only_movies_with_an_upcoming_screening()
    {
        var db = NewDb();
        db.Cinemas.Add(Cinema("cinema-farnese", "Cinema Farnese"));
        db.Movies.AddRange(Movie(1, "Showing Now"), Movie(2, "Not Scheduled"));
        db.Screenings.Add(Screening(1, 1, "cinema-farnese", Future));

        var corpus = await BuildAsync(db);

        Assert.Contains("## Showing Now", corpus);
        Assert.DoesNotContain("Not Scheduled", corpus);
        Assert.Contains("Totale: 1 film", corpus);
    }

    [Fact]
    public async Task Prefers_tmdb_title_and_appends_distinct_original_and_english_titles()
    {
        var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = new Movie
        {
            Id = 1,
            Title = "Titolo Scrape",
            TmdbTitle = "Titolo TMDB",
            OriginalTitle = "Original Name",
            TmdbTitleEn = "English Name",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
        };
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));

        var corpus = await BuildAsync(db);

        Assert.Contains("## Titolo TMDB (Original Name / EN: English Name)", corpus);
    }

    [Fact]
    public async Task Tags_ov_3d_and_open_air_and_groups_by_cinema()
    {
        var db = NewDb();
        db.Cinemas.AddRange(Cinema("arena", "Arena Estiva", openAir: true), Cinema("indoor", "Sala Chiusa"));
        db.Movies.Add(Movie(1, "Film"));
        db.Screenings.AddRange(
            Screening(1, 1, "arena", Future, isOV: true),
            Screening(2, 1, "indoor", Future, is3D: true));

        var corpus = await BuildAsync(db);

        Assert.Contains("Arena Estiva [Open Air]:", corpus);
        Assert.Contains("[VO]", corpus);
        Assert.Contains("Sala Chiusa:", corpus);
        Assert.Contains("[3D]", corpus);
    }

    [Fact]
    public async Task Renders_ratings_in_imdb_rt_mc_order_awards_and_caps_keywords_at_12()
    {
        var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = new Movie
        {
            Id = 1,
            Title = "Film",
            ImdbRating = "8.1",
            RtRating = "92%",
            MetacriticScore = "75",
            Awards = "Won 2 Oscars. 126 wins & 375 nominations total",
            TmdbKeywords = "[" + string.Join(",", Enumerable.Range(1, 15).Select(i => $"{{\"name\":\"k{i}\"}}")) + "]",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
        };
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));

        var corpus = await BuildAsync(db);

        Assert.Contains("IMDB: 8.1 | RT: 92% | MC: 75", corpus);
        Assert.Contains("Premi: Won 2 Oscars. 126 wins & 375 nominations total", corpus);
        Assert.Contains("k12", corpus);
        Assert.DoesNotContain("k13", corpus);
    }

    [Fact]
    public async Task Extracts_country_and_collection_names_from_json_blobs()
    {
        var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = new Movie
        {
            Id = 1,
            Title = "Film",
            TmdbProductionCountries = "[{\"iso_3166_1\":\"IT\",\"name\":\"Italy\"}]",
            TmdbCollection = "{\"id\":10,\"name\":\"Saga Collection\"}",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
        };
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));

        var corpus = await BuildAsync(db);

        Assert.Contains("Paese: Italy", corpus);
        Assert.Contains("Saga: Saga Collection", corpus);
    }

    [Fact]
    public async Task Tolerates_malformed_json_blobs()
    {
        var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = new Movie
        {
            Id = 1,
            Title = "Film",
            TmdbKeywords = "not json",
            TmdbCollection = "{bad}",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
        };
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));

        var corpus = await BuildAsync(db);

        Assert.Contains("## Film", corpus);
        Assert.DoesNotContain("Keywords:", corpus);
        Assert.DoesNotContain("Saga:", corpus);
    }

    [Fact]
    public async Task The_heading_names_the_city_and_the_italian_date()
    {
        var corpus = await BuildAsync(NewDb());

        Assert.StartsWith("# Film in programmazione a Roma — 2026-06-22\n", corpus);
    }

    [Fact]
    public void Display_title_prefers_tmdbs_and_ratings_prefer_imdb_then_rt_then_mc()
    {
        var movie = new Movie
        {
            Id = 1, Title = "Scraped", TmdbTitle = "", MetacriticScore = "70", RtRating = "90%",
            CreatedAt = Stamp, UpdatedAt = Stamp,
        };

        Assert.Equal("Scraped", CorpusRenderer.DisplayTitle(movie)); // an empty TMDB title doesn't count
        Assert.Equal(["RT: 90%", "MC: 70"], CorpusRenderer.PreferredRatings(movie));
    }

    [Fact]
    public async Task Empty_when_no_active_movies()
    {
        var db = NewDb();
        var corpus = await BuildAsync(db);

        Assert.Contains("Totale: 0 film", corpus);
    }

    private const string Stamp = "2026-01-01T00:00:00Z";

    private static Movie Movie(int id, string title) => new()
    {
        Id = id,
        Title = title,
        CreatedAt = Stamp,
        UpdatedAt = Stamp,
    };

    private static Cinema Cinema(string id, string name, bool openAir = false) => new()
    {
        Id = id,
        Name = name,
        OpenAir = openAir,
        CityId = City.RomeId,
        Country = "IT",
        Website = $"https://{id}.example.com",
        ScheduleUrl = $"https://{id}.example.com/s",
        CreatedAt = Stamp,
        UpdatedAt = Stamp,
    };

    private static Screening Screening(
        int id, int movieId, string cinemaId, string datetime, bool isOV = false, bool is3D = false) => new()
    {
        Id = id,
        MovieId = movieId,
        CinemaId = cinemaId,
        Datetime = datetime,
        IsOV = isOV,
        Is3D = is3D,
        CreatedAt = Stamp,
        UpdatedAt = Stamp,
    };
}
