using Felinoir.Application.Felix;
using Felinoir.Domain.Entities;
using Felinoir.Infrastructure.Persistence;
using Felinoir.UnitTests.TestSupport;

namespace Felinoir.UnitTests.Felix;

public class CorpusBuilderTests
{
    /// <summary>2026-06-22 20:30 in Rome (CEST, UTC+2).</summary>
    private static readonly DateTimeOffset Now = new(2026, 6, 22, 18, 30, 0, TimeSpan.Zero);

    // Screening times are Italian wall clock, as the scrapers store them.
    private const string Future = "2026-06-24T21:00:00";
    private const string Past = "2026-06-20T21:00:00";

    private const string Milan = "milan";

    /// <summary>An empty database with Rome and Milan.</summary>
    private static ApplicationDbContext NewDb()
    {
        var db = InMemoryDb.Create();
        db.Cities.AddRange(
            new City { Id = City.RomeId, Name = "Roma", CreatedAt = "x", UpdatedAt = "x" },
            new City { Id = Milan, Name = "Milano", CreatedAt = "x", UpdatedAt = "x" });
        db.SaveChanges();
        return db;
    }

    private static Task<string> BuildAsync(ApplicationDbContext db, string cityId = City.RomeId) =>
        new CorpusBuilder(db, new FixedTimeProvider(Now)).BuildAsync(cityId);

    [Fact]
    public async Task Includes_only_movies_with_a_future_screening()
    {
        using var db = NewDb();
        db.Cinemas.Add(Cinema("cinema-farnese", "Cinema Farnese"));
        db.Movies.AddRange(Movie(1, "Showing Now"), Movie(2, "Not Scheduled"), Movie(3, "Past Only"));
        db.Screenings.AddRange(
            Screening(1, 1, "cinema-farnese", Future),
            Screening(2, 3, "cinema-farnese", Past));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("## Showing Now", corpus);
        Assert.DoesNotContain("Not Scheduled", corpus);
        Assert.DoesNotContain("Past Only", corpus);
        Assert.Contains("Totale: 1 film", corpus);
    }

    [Fact]
    public async Task Prefers_tmdb_title_and_appends_distinct_original_and_english_titles()
    {
        using var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = Movie(1, "Titolo Scrape");
        m.TmdbTitle = "Titolo TMDB";
        m.OriginalTitle = "Original Name";
        m.TmdbTitleEn = "English Name";
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("## Titolo TMDB (Original Name / EN: English Name)", corpus);
    }

    [Fact]
    public async Task Tags_ov_3d_and_open_air_and_groups_by_cinema()
    {
        using var db = NewDb();
        var arena = Cinema("arena", "Arena Estiva");
        arena.OpenAir = true;
        db.Cinemas.AddRange(arena, Cinema("indoor", "Sala Chiusa"));
        db.Movies.Add(Movie(1, "Film"));
        var s1 = Screening(1, 1, "arena", Future);
        s1.IsOV = true;
        var s2 = Screening(2, 1, "indoor", Future);
        s2.Is3D = true;
        db.Screenings.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("Arena Estiva [Open Air]:", corpus);
        Assert.Contains("[VO]", corpus);
        Assert.Contains("Sala Chiusa:", corpus);
        Assert.Contains("[3D]", corpus);
    }

    [Fact]
    public async Task Renders_ratings_in_imdb_rt_mc_order_awards_and_caps_keywords_at_12()
    {
        using var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = Movie(1, "Film");
        m.ImdbRating = "8.1";
        m.RtRating = "92%";
        m.MetacriticScore = "75";
        m.Awards = "Won 2 Oscars. 126 wins & 375 nominations total";
        m.TmdbKeywords = "[" + string.Join(",", Enumerable.Range(1, 15).Select(i => $"{{\"name\":\"k{i}\"}}")) + "]";
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("IMDB: 8.1 | RT: 92% | MC: 75", corpus);
        Assert.Contains("Premi: Won 2 Oscars. 126 wins & 375 nominations total", corpus);
        Assert.Contains("k12", corpus);
        Assert.DoesNotContain("k13", corpus);
    }

    [Fact]
    public async Task Extracts_country_and_collection_names_from_json_blobs()
    {
        using var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = Movie(1, "Film");
        m.TmdbProductionCountries = "[{\"iso_3166_1\":\"IT\",\"name\":\"Italy\"}]";
        m.TmdbCollection = "{\"id\":10,\"name\":\"Saga Collection\"}";
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("Paese: Italy", corpus);
        Assert.Contains("Saga: Saga Collection", corpus);
    }

    [Fact]
    public async Task Tolerates_malformed_json_blobs()
    {
        using var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = Movie(1, "Film");
        m.TmdbKeywords = "not json";
        m.TmdbCollection = "{bad}";
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("## Film", corpus);
        Assert.DoesNotContain("Keywords:", corpus);
        Assert.DoesNotContain("Saga:", corpus);
    }

    [Fact]
    public async Task Compares_screenings_with_the_current_italian_time()
    {
        // At 20:30 in Rome: 20:00 has started, 21:00 hasn't. Compared with the UTC clock
        // (18:30), the 20:00 screening used to count as upcoming.
        using var db = NewDb();
        db.Cinemas.Add(Cinema("c", "Cine"));
        db.Movies.AddRange(Movie(1, "Started Half An Hour Ago"), Movie(2, "Starts At Nine"));
        db.Screenings.AddRange(
            Screening(1, 1, "c", "2026-06-22T20:00:00"),
            Screening(2, 2, "c", "2026-06-22T21:00:00"));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.DoesNotContain("Started Half An Hour Ago", corpus);
        Assert.Contains("## Starts At Nine", corpus);
        Assert.Contains("lun 22/06 21:00", corpus);
    }

    [Fact]
    public async Task Covers_only_the_cinemas_of_its_city()
    {
        using var db = NewDb();
        var milanCinema = Cinema("milano-1", "Cinema Milanese");
        milanCinema.CityId = Milan;
        db.Cinemas.AddRange(Cinema("c", "Cine Romano"), milanCinema);
        db.Movies.AddRange(Movie(1, "In Rome"), Movie(2, "Only In Milan"));
        db.Screenings.AddRange(
            Screening(1, 1, "c", Future),
            Screening(2, 2, "milano-1", Future),
            Screening(3, 1, "milano-1", Future));
        await db.SaveChangesAsync();

        var rome = await BuildAsync(db);
        var milan = await BuildAsync(db, Milan);

        Assert.StartsWith("# Film in programmazione a Roma — 2026-06-22", rome);
        Assert.Contains("## In Rome", rome);
        Assert.DoesNotContain("Only In Milan", rome);
        Assert.DoesNotContain("Cinema Milanese", rome);

        Assert.StartsWith("# Film in programmazione a Milano — 2026-06-22", milan);
        Assert.Contains("Totale: 2 film", milan);
        Assert.DoesNotContain("Cine Romano", milan);
    }

    [Fact]
    public async Task Empty_when_no_active_movies()
    {
        using var db = NewDb();
        var corpus = await BuildAsync(db);

        Assert.Contains("Totale: 0 film", corpus);
    }

    private static Movie Movie(int id, string title) => new()
    {
        Id = id,
        Title = title,
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };

    private static Cinema Cinema(string id, string name) => new()
    {
        Id = id,
        Name = name,
        CityId = City.RomeId,
        Country = "IT",
        Website = $"https://{id}.example.com",
        ScheduleUrl = $"https://{id}.example.com/s",
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };

    private static Screening Screening(int id, int movieId, string cinemaId, string datetime) => new()
    {
        Id = id,
        MovieId = movieId,
        CinemaId = cinemaId,
        Datetime = datetime,
        CreatedAt = "2026-01-01T00:00:00Z",
        UpdatedAt = "2026-01-01T00:00:00Z",
    };
}
