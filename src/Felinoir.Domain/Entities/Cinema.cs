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
    public string City { get; set; } = default!;
    public string Country { get; set; } = "IT";
    public string Website { get; set; } = default!;
    public string ScheduleUrl { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp stored as TEXT (never TIMESTAMPTZ).</summary>
    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;

    [JsonIgnore]
    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}
