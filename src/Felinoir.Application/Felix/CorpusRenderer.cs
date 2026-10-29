using System.Globalization;
using System.Text.Json;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;

namespace Felinoir.Application.Felix;

/// <summary>
/// Everything a city's Felix corpus is made from: the city's name, the time it is built at, the
/// movies (by id), the city's cinemas, and its screenings that haven't started at
/// <see cref="Now"/>. Loaded by <see cref="CorpusSnapshotLoader"/>; plain data, so the corpus can
/// be rendered and tested without a database.
/// </summary>
public sealed record CorpusSnapshot(
    string CityName,
    ScreeningTime Now,
    IReadOnlyList<Movie> Movies,
    IReadOnlyList<Cinema> Cinemas,
    IReadOnlyList<Screening> UpcomingScreenings);

/// <summary>
/// Renders a <see cref="CorpusSnapshot"/> as the Markdown corpus Felix answers from (see CONTEXT.md),
/// in the same form as the original Node backend: only movies in programmazione, titles by
/// <see cref="DisplayTitle"/>, ratings by <see cref="PreferredRatings"/>, screenings grouped per
/// cinema with [VO]/[3D]/[Open Air] tags. A pure function: the same snapshot gives the same text.
/// </summary>
public static class CorpusRenderer
{
    private const int MaxKeywords = 12;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    public static string Render(CorpusSnapshot snapshot)
    {
        var cinemaNames = snapshot.Cinemas.ToDictionary(c => c.Id, c => c.Name);
        var cinemaOpenAir = snapshot.Cinemas.ToDictionary(c => c.Id, c => c.OpenAir);

        var byMovie = snapshot.UpcomingScreenings
            .GroupBy(s => s.MovieId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var active = snapshot.Movies.Where(m => IsInProgrammazione(m, byMovie)).ToList();

        var lines = new List<string>
        {
            $"# Film in programmazione a {snapshot.CityName} — {snapshot.Now.ToString()[..10]}",
            $"Totale: {active.Count} film",
            "",
        };

        foreach (var m in active)
        {
            AppendMovie(lines, m, byMovie[m.Id], cinemaNames, cinemaOpenAir);
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    /// <summary>A movie is in programmazione when it has a screening in the city that hasn't started.</summary>
    public static bool IsInProgrammazione(Movie movie, IReadOnlyDictionary<int, List<Screening>> upcomingByMovie) =>
        upcomingByMovie.ContainsKey(movie.Id);

    private static void AppendMovie(
        List<string> lines,
        Movie m,
        List<Screening> movieScreenings,
        IReadOnlyDictionary<string, string> cinemaNames,
        IReadOnlyDictionary<string, bool> cinemaOpenAir)
    {
        var title = DisplayTitle(m);
        var orig = FirstNonEmpty(m.OriginalTitle, m.TmdbOriginalTitle);
        var titleEn = FirstNonEmpty(m.TmdbTitleEn);

        // Title line — append original/English titles only when they actually differ.
        var titleLine = $"## {title}";
        var subtitles = new List<string>();
        if (orig is not null && orig != title) subtitles.Add(orig);
        if (titleEn is not null && titleEn != title && titleEn != orig) subtitles.Add($"EN: {titleEn}");
        if (subtitles.Count > 0) titleLine += $" ({string.Join(" / ", subtitles)})";
        lines.Add(titleLine);

        if (FirstNonEmpty(m.TmdbTagline) is { } tagline) lines.Add($"\"{tagline}\"");

        var meta = new List<string>();
        if (FirstNonEmpty(m.TmdbGenre, m.Genres) is { } genres) meta.Add($"Generi: {genres}");
        if (FirstNonEmpty(m.Released) is { } released) meta.Add($"Anno: {released[..Math.Min(4, released.Length)]}");
        if (m.DurationMinutes is { } duration) meta.Add($"Durata: {duration} min");
        if (FirstNonEmpty(m.TmdbLanguage) is { } lang) meta.Add($"Lingua: {lang.ToUpperInvariant()}");
        if (FirstNonEmpty(m.ContentRating) is { } cert) meta.Add($"Cert: {cert}");
        var countries = ParseJsonNames(m.TmdbProductionCountries);
        if (countries.Count > 0) meta.Add($"Paese: {string.Join(", ", countries)}");
        if (meta.Count > 0) lines.Add(string.Join(" | ", meta));

        var crew = new List<string>();
        if (FirstNonEmpty(m.Director) is { } dir) crew.Add($"Dir: {dir}");
        if (FirstNonEmpty(m.Writer) is { } writer) crew.Add($"Script: {writer}");
        if (FirstNonEmpty(m.Actors) is { } actors) crew.Add($"Cast: {actors}");
        if (crew.Count > 0) lines.Add(string.Join(" | ", crew));

        if (ParseJsonName(m.TmdbCollection) is { } saga) lines.Add($"Saga: {saga}");

        var ratings = PreferredRatings(m);
        if (ratings.Count > 0) lines.Add(string.Join(" | ", ratings));

        if (FirstNonEmpty(m.Awards) is { } awards) lines.Add($"Premi: {awards}");

        var keywords = ParseJsonNames(m.TmdbKeywords);
        if (keywords.Count > 0)
            lines.Add($"Keywords: {string.Join(", ", keywords.Take(MaxKeywords))}");

        if (FirstNonEmpty(m.TmdbPlot, m.TmdbPlotEn, m.Synopsis) is { } plot) lines.Add($"Trama: {plot}");

        AppendScreenings(lines, movieScreenings, cinemaNames, cinemaOpenAir);
    }

    private static void AppendScreenings(
        List<string> lines,
        List<Screening> movieScreenings,
        IReadOnlyDictionary<string, string> cinemaNames,
        IReadOnlyDictionary<string, bool> cinemaOpenAir)
    {
        // Group by cinema, preserving first-seen order to match the Node output.
        var byCinema = new Dictionary<string, (string CinemaId, List<string> Times)>();
        var order = new List<string>();

        foreach (var s in movieScreenings)
        {
            var name = cinemaNames.GetValueOrDefault(s.CinemaId, s.CinemaId);
            var (date, time) = ScreeningTime.TryParse(s.Datetime, out var at)
                ? (at.WallClock.ToString("ddd dd/MM", Italian), at.WallClock.ToString("HH:mm", Italian))
                : (s.Datetime, "");
            var tag = $"{(s.IsOV ? " [VO]" : "")}{(s.Is3D ? " [3D]" : "")}";

            if (!byCinema.TryGetValue(name, out var entry))
            {
                entry = (s.CinemaId, new List<string>());
                byCinema[name] = entry;
                order.Add(name);
            }

            entry.Times.Add($"{date} {time}{tag}");
        }

        lines.Add("Proiezioni:");
        foreach (var name in order)
        {
            var (cinemaId, times) = byCinema[name];
            var label = cinemaOpenAir.GetValueOrDefault(cinemaId) ? $"{name} [Open Air]" : name;
            lines.Add($"  {label}: {string.Join(", ", times)}");
        }
    }

    /// <summary>The title Felix calls a film by: TMDB's (Italian) title, else the scraped one.</summary>
    public static string DisplayTitle(Movie movie) => FirstNonEmpty(movie.TmdbTitle, movie.Title) ?? movie.Title;

    /// <summary>The ratings Felix quotes, in order of preference: IMDb, Rotten Tomatoes, Metacritic.</summary>
    public static IReadOnlyList<string> PreferredRatings(Movie movie)
    {
        var ratings = new List<string>();
        if (FirstNonEmpty(movie.ImdbRating) is { } imdb) ratings.Add($"IMDB: {imdb}");
        if (FirstNonEmpty(movie.RtRating) is { } rt) ratings.Add($"RT: {rt}");
        if (FirstNonEmpty(movie.MetacriticScore) is { } mc) ratings.Add($"MC: {mc}");
        return ratings;
    }

    /// <summary>Returns the first argument that is neither null nor empty, mirroring JS <c>||</c>.</summary>
    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

    /// <summary>Extracts the <c>name</c> values from a JSON array of objects; tolerant of malformed input.</summary>
    private static List<string> ParseJsonNames(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            var names = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Object
                    && el.TryGetProperty("name", out var n)
                    && n.ValueKind == JsonValueKind.String
                    && n.GetString() is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }
            return names;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Extracts <c>name</c> from a single JSON object (e.g. tmdb collection); null if absent/malformed.</summary>
    private static string? ParseJsonName(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("name", out var n)
                && n.ValueKind == JsonValueKind.String)
            {
                return FirstNonEmpty(n.GetString());
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
