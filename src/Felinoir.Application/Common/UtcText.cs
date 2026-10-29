using System.Globalization;

namespace Felinoir.Application.Common;

/// <summary>The API's UTC timestamp text, e.g. <c>2026-10-17T19:00:00Z</c>.</summary>
public static class UtcText
{
    public static string Format(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
