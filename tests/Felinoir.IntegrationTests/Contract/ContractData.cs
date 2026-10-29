using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Contract;

/// <summary>
/// The fixed dataset the contract snapshots are taken against. Never change existing values
/// casually: every snapshot depends on them. Add new rows at the end instead.
///
/// Screenings are in 2099, at Italian local time as the scrapers write them. The API runs on
/// <see cref="Now"/>, a frozen clock on the morning of the first screening day, so one
/// screening (<see cref="MorningScreening"/>) has already started.
/// </summary>
internal static class ContractData
{
    /// <summary>2099-06-01 10:00 in Rome (CEST).</summary>
    public static readonly DateTimeOffset Now = new(2099, 6, 1, 8, 0, 0, TimeSpan.Zero);

    public const int Dune = 1;
    public const int Shorts = 2;
    public const int Chimera = 3;
    public const int NotShowing = 4;

    // Every cinema is listed under Rome, the only city.
    public const string Adriano = "cinema-adriano";
    public const string SanCosimato = "san-cosimato";    // open-air
    public const string Augustus = "multiplex-augustus"; // in Velletri, outside the municipality

    public const int EveningScreening = 1;
    public const int OpenAirScreening = 2;
    public const int VelletriScreening = 3;
    public const int MorningScreening = 4; // started before Now
    public const int OldScreening = 5;     // years in the past

    public static object[] All() =>
    [
        Cinema(Adriano, "Cinema Adriano", chain: "NVR", address: "Piazza Cavour, 22, 00193 Roma RM"),
        Cinema(SanCosimato, "Piazza San Cosimato", openAir: true),
        Cinema(Augustus, "Multiplex Augustus", address: "Viale Oberdan, 00049 Velletri RM"),

        DuneMovie(),
        TestData.Movie(Shorts, title: "Cortometraggi d'autore", createdAt: "2026-02-01T09:00:00Z"),
        ChimeraMovie(),
        TestData.Movie(NotShowing, title: "Fuori programmazione", slug: "fuori-programmazione",
            createdAt: "2026-01-15T09:00:00Z"),

        new Screening
        {
            Id = EveningScreening, MovieId = Dune, CinemaId = Adriano, Datetime = "2099-06-01T21:00:00",
            Hall = "Sala 1",
            IsOV = true,
            SubtitleLanguage = "it",
            BookingUrl = "https://www.nvrcinema.it/adriano/book/1",
            Meta = """{"price":"8.50","source":"nvr"}""",
            CreatedAt = TestData.Created, UpdatedAt = TestData.Created,
        },
        new Screening
        {
            Id = OpenAirScreening, MovieId = Dune, CinemaId = SanCosimato, Datetime = "2099-06-02T21:30:00",
            Is3D = true,
            CreatedAt = TestData.Created, UpdatedAt = TestData.Created,
        },
        TestData.Screening(VelletriScreening, Shorts, Augustus, "2099-06-03T18:00:00"),
        TestData.Screening(MorningScreening, Chimera, Adriano, "2099-06-01T09:00:00"),
        TestData.Screening(OldScreening, Dune, Adriano, "2020-01-01T20:00:00"),
    ];

    private static Cinema Cinema(
        string id, string name, string? chain = null, string? address = null, bool openAir = false) => new()
    {
        Id = id,
        Name = name,
        Chain = chain,
        Address = address,
        OpenAir = openAir,
        CityId = City.RomeId,
        Country = "IT",
        Website = $"https://{id}.example.com",
        ScheduleUrl = $"https://{id}.example.com/schedule",
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
    };

    /// <summary>Every column filled, including the TMDB JSON blobs.</summary>
    private static Movie DuneMovie() => new()
    {
        Id = Dune,
        Title = "Dune - Parte due",
        Slug = "dune-parte-due",
        CreatedAt = "2026-03-01T09:00:00Z",
        UpdatedAt = "2026-03-01T09:00:00Z",
        OriginalTitle = "Dune: Part Two",
        Director = "Denis Villeneuve",
        DurationMinutes = 166,
        Genres = "Fantascienza, Avventura",
        Synopsis = "Paul Atreides si unisce ai Fremen.",
        PosterUrl = "https://scraped.example/dune.jpg",
        TrailerUrl = "https://scraped.example/dune-trailer",
        ExternalId = "nvr-123",
        Rating = "T",
        TmdbId = 693134,
        ImdbId = "tt15239678",
        TmdbRating = "8.1",
        TmdbVotes = "6500",
        TmdbTitle = "Dune - Parte due",
        TmdbTitleEn = "Dune: Part Two",
        TmdbOriginalTitle = "Dune: Part Two",
        TmdbPlot = "Paul Atreides si unisce a Chani e ai Fremen.",
        TmdbPlotEn = "Paul Atreides unites with Chani and the Fremen.",
        TmdbGenre = "Fantascienza",
        TmdbGenreEn = "Science Fiction",
        TmdbLanguage = "en",
        TmdbCountry = "US",
        TmdbPosterUrl = "https://image.tmdb.org/t/p/w500/dune.jpg",
        TmdbTrailerUrlIt = "https://youtube.com/watch?v=it",
        TmdbTrailerUrlEn = "https://youtube.com/watch?v=en",
        TmdbPopularity = "512.3",
        TmdbStatus = "Released",
        TmdbTagline = "Long live the fighters.",
        TmdbBudget = 190_000_000,
        TmdbRevenue = 714_444_358,
        TmdbHomepage = "https://dune.example",
        TmdbCollection = """{"id":726871,"name":"Dune Collection"}""",
        TmdbProductionCompanies = """[{"id":923,"name":"Legendary Pictures"}]""",
        TmdbProductionCountries = """[{"iso_3166_1":"US","name":"United States of America"}]""",
        TmdbSpokenLanguages = """[{"iso_639_1":"en","name":"English"}]""",
        TmdbBackdrops = """["https://image.tmdb.org/t/p/w1280/b1.jpg"]""",
        TmdbVideos = """[{"key":"abc","site":"YouTube"}]""",
        TmdbKeywords = """["desert","prophecy","sandworm"]""",
        TmdbRecommendations = """[{"id":438631,"title":"Dune"}]""",
        TmdbWatchProviders = """{"IT":{"flatrate":["Netflix"]}}""",
        TmdbCredits = """{"cast":[{"name":"Timothée Chalamet","character":"Paul Atreides"}],"crew":[{"name":"Denis Villeneuve","job":"Director"}]}""",
        TmdbEnrichedAt = "2026-03-02T03:00:00Z",
        ContentRating = "PG-13",
        Writer = "Denis Villeneuve, Jon Spaihts",
        Actors = "Timothée Chalamet, Zendaya",
        Released = "2024-02-28",
        ImdbRating = "8.5",
        ImdbVotes = "620,000",
        RtRating = "92%",
        MetacriticScore = "79",
        Awards = "Won 2 Oscars.",
        OmdbEnrichedAt = "2026-03-02T04:00:00Z",
    };

    /// <summary>Same creation time as Dune (tests the id tie-break); RT rating only; no TMDB title.</summary>
    private static Movie ChimeraMovie() => new()
    {
        Id = Chimera,
        Title = "La chimera",
        Slug = "la-chimera",
        CreatedAt = "2026-03-01T09:00:00Z",
        UpdatedAt = "2026-03-01T09:00:00Z",
        Director = "Alice Rohrwacher",
        DurationMinutes = 130,
        RtRating = "97%",
        TmdbKeywords = """["etruscans","tomb raiders"]""",
    };
}
