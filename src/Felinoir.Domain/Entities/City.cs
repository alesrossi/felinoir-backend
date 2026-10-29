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

    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;

    /// <summary>ISO 8601 UTC timestamp stored as TEXT (never TIMESTAMPTZ).</summary>
    public string CreatedAt { get; init; } = default!;
    public string UpdatedAt { get; init; } = default!;

    public ICollection<Cinema> Cinemas { get; init; } = new List<Cinema>();
}
