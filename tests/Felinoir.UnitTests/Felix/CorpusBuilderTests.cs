using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Felix;
using Felinoir.Domain.Entities;
using Felinoir.Infrastructure.Persistence;
using Felinoir.UnitTests.TestSupport;

namespace Felinoir.UnitTests.Felix;

public class CorpusBuilderTests
{
    private static readonly string Future = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ");
    private static readonly string Past = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private sealed class StubConfig(params CinemaConfigEntry[] entries) : ICinemaConfigProvider
    {
        public IReadOnlyList<CinemaConfigEntry> GetAll() => entries;
    }

    private static async Task<string> BuildAsync(
        ApplicationDbContext db, params CinemaConfigEntry[] config)
    {
        return await new CorpusBuilder(db, new StubConfig(config)).BuildAsync();
    }

    [Fact]
    public async Task Includes_only_movies_with_a_future_screening()
    {
        using var db = InMemoryDb.Create();
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
        using var db = InMemoryDb.Create();
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
        using var db = InMemoryDb.Create();
        db.Cinemas.AddRange(Cinema("arena", "Arena Estiva"), Cinema("indoor", "Sala Chiusa"));
        db.Movies.Add(Movie(1, "Film"));
        var s1 = Screening(1, 1, "arena", Future);
        s1.IsOV = true;
        var s2 = Screening(2, 1, "indoor", Future);
        s2.Is3D = true;
        db.Screenings.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db, new CinemaConfigEntry("arena", "Arena Estiva", OpenAir: true));

        Assert.Contains("Arena Estiva [Open Air]:", corpus);
        Assert.Contains("[VO]", corpus);
        Assert.Contains("Sala Chiusa:", corpus);
        Assert.Contains("[3D]", corpus);
    }

    [Fact]
    public async Task Renders_ratings_in_imdb_rt_mc_order_and_caps_keywords_at_12()
    {
        using var db = InMemoryDb.Create();
        db.Cinemas.Add(Cinema("c", "Cine"));
        var m = Movie(1, "Film");
        m.ImdbRating = "8.1";
        m.RtRating = "92%";
        m.MetacriticScore = "75";
        m.TmdbKeywords = "[" + string.Join(",", Enumerable.Range(1, 15).Select(i => $"{{\"name\":\"k{i}\"}}")) + "]";
        db.Movies.Add(m);
        db.Screenings.Add(Screening(1, 1, "c", Future));
        await db.SaveChangesAsync();

        var corpus = await BuildAsync(db);

        Assert.Contains("IMDB: 8.1 | RT: 92% | MC: 75", corpus);
        Assert.Contains("k12", corpus);
        Assert.DoesNotContain("k13", corpus);
    }

    [Fact]
    public async Task Extracts_country_and_collection_names_from_json_blobs()
    {
        using var db = InMemoryDb.Create();
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
        using var db = InMemoryDb.Create();
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
    public async Task Empty_when_no_active_movies()
    {
        using var db = InMemoryDb.Create();
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
        City = "Roma",
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
