namespace Felinoir.Domain.Entities;

/// <summary>
/// A physical cinema venue. <see cref="Id"/> is a human-readable slug
/// (e.g. "cinema-farnese") and is referenced by <see cref="Screening.CinemaId"/>. Written by the
/// scrapers; this server only reads it, so it is init-only.
/// </summary>
public class Cinema
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Chain { get; init; }
    public string? Address { get; init; }
    public string Country { get; init; } = "IT";
    public string Website { get; init; } = default!;
    public string ScheduleUrl { get; init; } = default!;

    /// <summary>ISO 8601 UTC timestamp stored as TEXT (never TIMESTAMPTZ).</summary>
    public string CreatedAt { get; init; } = default!;
    public string UpdatedAt { get; init; } = default!;

    /// <summary>The city the cinema is listed under, not necessarily the town in its address.</summary>
    public string CityId { get; init; } = default!;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>An outdoor venue (summer arenas); tagged as such in Felix's corpus.</summary>
    public bool OpenAir { get; init; }

    public City? City { get; init; }

    public ICollection<Screening> Screenings { get; init; } = new List<Screening>();
}
