using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Felinoir.Application.Features.Felix;

/// <summary>
/// Renders active UI filters as the plain-text block described in SPEC.md, appended to
/// the user's message so Felix restricts its answer to matching films/screenings.
/// Returns null when there are no active filters. <c>today</c> is the Italian date, for labelling.
/// </summary>
public static class FelixFilterFormatter
{
    public static string? Format(FelixFilters? filters, DateOnly today)
    {
        if (filters is null) return null;

        var lines = new List<string>();
        if (HasItems(filters.Cinemas)) lines.Add($"- Cinemas: {string.Join(", ", filters.Cinemas)}");
        if (HasItems(filters.Dates)) lines.Add($"- Dates: {string.Join(", ", filters.Dates.Select(d => LabelDate(d, today)))}");
        if (HasItems(filters.Times)) lines.Add($"- Times: {string.Join(", ", filters.Times)}");
        if (HasItems(filters.Genres)) lines.Add($"- Genres: {string.Join(", ", filters.Genres)}");
        if (filters.Ov == true) lines.Add("- Original version (no dubbing) only");
        if (!string.IsNullOrWhiteSpace(filters.Q)) lines.Add($"- Search: {filters.Q}");

        if (lines.Count == 0) return null;

        return string.Join("\n",
            ["[Note: I currently have these filters active on felinoir.it]", .. lines,
                "Please only include films and screenings that match ALL of these filters."]);
    }

    private static bool HasItems([NotNullWhen(true)] IReadOnlyList<string>? values) =>
        values is { Count: > 0 } && values.Any(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Labels today's date as "today (yyyy-MM-dd)", matching the SPEC.md example.</summary>
    private static string LabelDate(string date, DateOnly today) =>
        date == today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ? $"today ({date})" : date;
}
