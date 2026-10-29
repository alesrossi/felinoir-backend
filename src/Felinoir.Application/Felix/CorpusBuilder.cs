using System.Globalization;
using System.Text.Json;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Felinoir.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Felix;

/// <summary>
/// Builds the Markdown corpus of films currently showing in one city. Ported from the
/// original Node backend so Felix's answers stay consistent: only films with a future
/// screening are included, titles prefer the TMDB form, ratings follow IMDb → RT → MC,
/// and screenings are grouped per cinema with [VO]/[3D]/[Open Air] tags.
/// </summary>
public sealed class CorpusBuilder : ICorpusBuilder
{
    private const int MovieLimit = 1000;
    private const int ScreeningLimit = 100_000;
    private const int MaxKeywords = 12;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;

    public CorpusBuilder(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<string> BuildAsync(string cityId, CancellationToken ct = default)
    {
        // Screening times are Italian wall clock, so "now" must be too for the text comparison.
        var now = ScreeningTime.FromInstant(_time.GetUtcNow()).ToString();

        var cityName = await _db.Cities.AsNoTracking()
            .Where(c => c.Id == cityId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? cityId;

        var movies = await _db.Movies.AsNoTracking()
            .OrderBy(m => m.Id)
            .Take(MovieLimit)
            .ToListAsync(ct);

        var screenings = await _db.Screenings.AsNoTracking()
            .Where(s => s.Cinema!.CityId == cityId && string.Compare(s.Datetime, now) >= 0)
            .Take(ScreeningLimit)
            .ToListAsync(ct);

        var cinemas = await _db.Cinemas.AsNoTracking().Where(c => c.CityId == cityId).ToListAsync(ct);
        var cinemaNames = cinemas.ToDictionary(c => c.Id, c => c.Name);
        var cinemaOpenAir = cinemas.ToDictionary(c => c.Id, c => c.OpenAir);

        var byMovie = screenings
            .GroupBy(s => s.MovieId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var active = movies.Where(m => byMovie.ContainsKey(m.Id)).ToList();

        var lines = new List<string>
        {
            $"# Film in programmazione a {cityName} — {now[..10]}",
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

    private static void AppendMovie(
        List<string> lines,
        Movie m,
        List<Screening> movieScreenings,
        IReadOnlyDictionary<string, string> cinemaNames,
        IReadOnlyDictionary<string, bool> cinemaOpenAir)
    {
        var title = FirstNonEmpty(m.TmdbTitle, m.Title) ?? m.Title;
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

        var ratings = new List<string>();
        if (FirstNonEmpty(m.ImdbRating) is { } imdb) ratings.Add($"IMDB: {imdb}");
        if (FirstNonEmpty(m.RtRating) is { } rt) ratings.Add($"RT: {rt}");
        if (FirstNonEmpty(m.MetacriticScore) is { } mc) ratings.Add($"MC: {mc}");
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
