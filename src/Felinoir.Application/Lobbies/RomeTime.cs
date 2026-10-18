using System.Globalization;

namespace Felinoir.Application.Lobbies;

/// <summary>
/// Conversions between the scraper's screening times (Rome wall clock, no offset, e.g.
/// <c>2026-10-17T21:00:00</c>) and the UTC instants stored in <c>lobbies.starts_at</c>.
/// </summary>
public static class RomeTime
{
    private const string WallClockFormat = "yyyy-MM-dd'T'HH:mm:ss";
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    /// <summary>
    /// Reads a screening datetime as Rome wall clock. A value that carries its own offset
    /// or <c>Z</c> is taken as is. In the autumn DST overlap the standard (winter) offset is used.
    /// </summary>
    public static bool TryParseWallClock(string value, out DateTimeOffset utc)
    {
        utc = default;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return false;

        utc = dt.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(dt, Zone.GetUtcOffset(dt)).ToUniversalTime()
            : new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero);
        return true;
    }

    public static string ToWallClock(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Zone).ToString(WallClockFormat, CultureInfo.InvariantCulture);

    public static string ToUtcString(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString(UtcFormat, CultureInfo.InvariantCulture);
}
