using System.Text.Json.Serialization;

namespace Felinoir.Domain.Entities;

/// <summary>
/// A physical cinema venue. <see cref="Id"/> is a human-readable slug
/// (e.g. "cinema-farnese") and is referenced by <see cref="Screening.CinemaId"/>.
/// </summary>
public class Cinema
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Chain { get; set; }
    public string? Address { get; set; }

    /// <summary>
    /// The <see cref="Entities.City"/>'s display name, serialised as <c>city</c> as before cities
    /// had their own table. Needs <see cref="City"/> loaded; queries that return cinemas include it.
    /// </summary>
    [JsonPropertyName("city")]
    public string? CityName => City?.Name;

    public string Country { get; set; } = "IT";
    public string Website { get; set; } = default!;
    public string ScheduleUrl { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp stored as TEXT (never TIMESTAMPTZ).</summary>
    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;

    /// <summary>The city the cinema is listed under, not necessarily the town in its address.</summary>
    [JsonIgnore]
    public string CityId { get; set; } = default!;

    [JsonIgnore]
    public double? Latitude { get; set; }

    [JsonIgnore]
    public double? Longitude { get; set; }

    /// <summary>An outdoor venue (summer arenas); tagged as such in Felix's corpus.</summary>
    [JsonIgnore]
    public bool OpenAir { get; set; }

    [JsonIgnore]
    public City? City { get; set; }

    [JsonIgnore]
    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
