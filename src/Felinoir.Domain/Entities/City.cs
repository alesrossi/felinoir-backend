using System.Text.Json.Serialization;

namespace Felinoir.Domain.Entities;

/// <summary>
/// An Italian city that cinemas are listed under and that a user browses (see CONTEXT.md).
/// Reference data: rows are added by migrations, never by the scrapers. <see cref="Id"/> is a
/// slug such as <c>rome</c>; <see cref="Name"/> is what users see, such as <c>Roma</c>.
/// </summary>
public class City
{
    /// <summary>Today the only city; every cinema is listed under it.</summary>
    public const string RomeId = "rome";

    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp stored as TEXT (never TIMESTAMPTZ).</summary>
    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;

    [JsonIgnore]
    public ICollection<Cinema> Cinemas { get; set; } = new List<Cinema>();
}
