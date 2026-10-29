using System.Globalization;

namespace Felinoir.Domain.ValueObjects;

/// <summary>
/// When a screening starts, as Italian wall-clock time with no offset: the form the scrapers
/// store in <c>screenings.datetime</c>, e.g. <c>2026-10-17T21:00:00</c>. Every city is in Italy,
/// so there is one time zone, <c>Europe/Rome</c>.
///
/// The canonical text has seconds, which makes text order chronological order; that is how
/// the database compares these values. One scraper (Troisi) writes minutes only
/// (<c>2026-10-17T21:00</c>), which parses but is stored as written.
/// </summary>
public readonly record struct ScreeningTime : IComparable<ScreeningTime>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss";
    private static readonly string[] StoredFormats = [Format, "yyyy-MM-dd'T'HH:mm"];

    private static readonly TimeZoneInfo Italy = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    private ScreeningTime(DateTime wallClock) =>
        WallClock = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

    /// <summary>The Italian wall-clock date and time.</summary>
    public DateTime WallClock { get; }

    /// <summary>Reads a stored screening time; anything with an offset or another shape is rejected.</summary>
    public static bool TryParse(string? text, out ScreeningTime time)
    {
        var ok = DateTime.TryParseExact(text, StoredFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var wallClock);
        time = ok ? new ScreeningTime(wallClock) : default;
        return ok;
    }

    /// <summary>
    /// Reads a filter bound such as <c>?from=</c>: Italian wall-clock time when it has no offset,
    /// or any ISO 8601 instant with <c>Z</c> or an offset, converted to Italian time.
    /// </summary>
    public static bool TryParseBound(string? text, out ScreeningTime time)
    {
        time = default;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return false;

        time = parsed.Kind == DateTimeKind.Unspecified
            ? new ScreeningTime(parsed)
            : FromInstant(new DateTimeOffset(parsed.ToUniversalTime(), TimeSpan.Zero));
        return true;
    }

    /// <summary>The Italian wall-clock time at <paramref name="instant"/>.</summary>
    public static ScreeningTime FromInstant(DateTimeOffset instant) =>
        new(TimeZoneInfo.ConvertTime(instant, Italy).DateTime);

    /// <summary>
    /// The instant this wall-clock time denotes. In the autumn hour that happens twice, and the
    /// spring hour that never happens, the standard (winter) offset is used.
    /// </summary>
    public DateTimeOffset ToInstant() =>
        new DateTimeOffset(WallClock, Italy.GetUtcOffset(WallClock)).ToUniversalTime();

    public int CompareTo(ScreeningTime other) => WallClock.CompareTo(other.WallClock);

    public static bool operator <(ScreeningTime left, ScreeningTime right) => left.CompareTo(right) < 0;
    public static bool operator >(ScreeningTime left, ScreeningTime right) => left.CompareTo(right) > 0;
    public static bool operator <=(ScreeningTime left, ScreeningTime right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ScreeningTime left, ScreeningTime right) => left.CompareTo(right) >= 0;

    /// <summary>The canonical text, with seconds: <c>2026-10-17T21:00:00</c>.</summary>
    public override string ToString() => WallClock.ToString(Format, CultureInfo.InvariantCulture);
}
