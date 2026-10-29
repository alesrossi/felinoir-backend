using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Contract;

/// <summary>
/// The fixed dataset the contract snapshots are taken against. Never change existing values
/// casually: every snapshot depends on them. Add new rows at the end instead.
///
/// Screenings are in 2099 (Italian local time, as the scrapers write them), so they stay
/// "upcoming" for the real clock that Felix's corpus still reads. Lobby tests run on
/// <see cref="Now"/>, a frozen clock on the morning of the first screening day.
/// </summary>
internal static class ContractData
{
    /// <summary>2099-06-01 10:00 in Rome (CEST).</summary>
    public static readonly DateTimeOffset Now = new(2099, 6, 1, 8, 0, 0, TimeSpan.Zero);

    public const int Dune = 1;
    public const int Shorts = 2;
    public const int Chimera = 3;
    public const int NotShowing = 4;

    public const string Adriano = "cinema-adriano";      // in cinemas.yml, not open-air
    public const string SanCosimato = "san-cosimato";    // in cinemas.yml, open-air
    public const string Augustus = "multiplex-augustus"; // not in cinemas.yml, in Velletri

    public const int EveningScreening = 1;
    public const int OpenAirScreening = 2;
    public const int VelletriScreening = 3;
    public const int MorningScreening = 4; // started before Now
    public const int OldScreening = 5;     // years in the past

    public static object[] All() =>
    [
        Cinema(Adriano, "Cinema Adriano", "Roma", chain: "NVR", address: "Piazza Cavour, 22, 00193 Roma RM"),
        Cinema(SanCosimato, "Piazza San Cosimato", "Roma"),
        Cinema(Augustus, "Multiplex Augustus", "Velletri", address: "Viale Oberdan, 00049 Velletri RM"),

        DuneMovie(),
        TestData.Movie(Shorts, title: "Cortometraggi d'autore", createdAt: "2026-02-01T09:00:00Z"),
        ChimeraMovie(),
        TestData.Movie(NotShowing, title: "Fuori programmazione", slug: "fuori-programmazione",
            createdAt: "2026-01-15T09:00:00Z"),

        Screening(EveningScreening, Dune, Adriano, "2099-06-01T21:00:00", s =>
        {
            s.Hall = "Sala 1";
            s.IsOV = true;
            s.SubtitleLanguage = "it";
            s.BookingUrl = "https://www.nvrcinema.it/adriano/book/1";
            s.Meta = """{"price":"8.50","source":"nvr"}""";
        }),
        Screening(OpenAirScreening, Dune, SanCosimato, "2099-06-02T21:30:00", s => s.Is3D = true),
        Screening(VelletriScreening, Shorts, Augustus, "2099-06-03T18:00:00"),
        Screening(MorningScreening, Chimera, Adriano, "2099-06-01T09:00:00"),
        Screening(OldScreening, Dune, Adriano, "2020-01-01T20:00:00"),
    ];

    private static Cinema Cinema(
        string id, string name, string city, string? chain = null, string? address = null)
    {
        var cinema = TestData.Cinema(id, name: name, city: city);
        cinema.Chain = chain;
        cinema.Address = address;
        return cinema;
    }

    private static Screening Screening(
        int id, int movieId, string cinemaId, string datetime, Action<Screening>? configure = null)
    {
        var screening = TestData.Screening(id, movieId, cinemaId, datetime);
        configure?.Invoke(screening);
        return screening;
    }

    /// <summary>Every column filled, including the TMDB JSON blobs.</summary>
    private static Movie DuneMovie()
    {
        var m = TestData.Movie(Dune, title: "Dune - Parte due", slug: "dune-parte-due",
            createdAt: "2026-03-01T09:00:00Z");
        m.OriginalTitle = "Dune: Part Two";
        m.Director = "Denis Villeneuve";
        m.DurationMinutes = 166;
        m.Genres = "Fantascienza, Avventura";
        m.Synopsis = "Paul Atreides si unisce ai Fremen.";
        m.PosterUrl = "https://scraped.example/dune.jpg";
        m.TrailerUrl = "https://scraped.example/dune-trailer";
        m.ExternalId = "nvr-123";
        m.Rating = "T";
        m.TmdbId = 693134;
        m.ImdbId = "tt15239678";
        m.TmdbRating = "8.1";
        m.TmdbVotes = "6500";
        m.TmdbTitle = "Dune - Parte due";
        m.TmdbTitleEn = "Dune: Part Two";
        m.TmdbOriginalTitle = "Dune: Part Two";
        m.TmdbPlot = "Paul Atreides si unisce a Chani e ai Fremen.";
        m.TmdbPlotEn = "Paul Atreides unites with Chani and the Fremen.";
        m.TmdbGenre = "Fantascienza";
        m.TmdbGenreEn = "Science Fiction";
        m.TmdbLanguage = "en";
        m.TmdbCountry = "US";
        m.TmdbPosterUrl = "https://image.tmdb.org/t/p/w500/dune.jpg";
        m.TmdbTrailerUrlIt = "https://youtube.com/watch?v=it";
        m.TmdbTrailerUrlEn = "https://youtube.com/watch?v=en";
        m.TmdbPopularity = "512.3";
        m.TmdbStatus = "Released";
        m.TmdbTagline = "Long live the fighters.";
        m.TmdbBudget = 190_000_000;
        m.TmdbRevenue = 714_444_358;
        m.TmdbHomepage = "https://dune.example";
        m.TmdbCollection = """{"id":726871,"name":"Dune Collection"}""";
        m.TmdbProductionCompanies = """[{"id":923,"name":"Legendary Pictures"}]""";
        m.TmdbProductionCountries = """[{"iso_3166_1":"US","name":"United States of America"}]""";
        m.TmdbSpokenLanguages = """[{"iso_639_1":"en","name":"English"}]""";
        m.TmdbBackdrops = """["https://image.tmdb.org/t/p/w1280/b1.jpg"]""";
        m.TmdbVideos = """[{"key":"abc","site":"YouTube"}]""";
        m.TmdbKeywords = """["desert","prophecy","sandworm"]""";
        m.TmdbRecommendations = """[{"id":438631,"title":"Dune"}]""";
        m.TmdbWatchProviders = """{"IT":{"flatrate":["Netflix"]}}""";
        m.TmdbCredits = """{"cast":[{"name":"Timothée Chalamet","character":"Paul Atreides"}],"crew":[{"name":"Denis Villeneuve","job":"Director"}]}""";
        m.TmdbEnrichedAt = "2026-03-02T03:00:00Z";
        m.ContentRating = "PG-13";
        m.Writer = "Denis Villeneuve, Jon Spaihts";
        m.Actors = "Timothée Chalamet, Zendaya";
        m.Released = "2024-02-28";
        m.ImdbRating = "8.5";
        m.ImdbVotes = "620,000";
        m.RtRating = "92%";
        m.MetacriticScore = "79";
        m.Awards = "Won 2 Oscars.";
        m.OmdbEnrichedAt = "2026-03-02T04:00:00Z";
        return m;
    }

    /// <summary>Same creation time as Dune (tests the id tie-break); RT rating only; no TMDB title.</summary>
    private static Movie ChimeraMovie()
    {
        var m = TestData.Movie(Chimera, title: "La chimera", slug: "la-chimera",
            createdAt: "2026-03-01T09:00:00Z");
        m.Director = "Alice Rohrwacher";
        m.DurationMinutes = 130;
        m.RtRating = "97%";
        m.TmdbKeywords = """["etruscans","tomb raiders"]""";
        return m;
    }
}
