namespace Felinoir.Application.Screenings;

/// <summary>
/// Filter/pagination criteria for <see cref="IScreeningService.GetAsync"/>.
/// <see cref="From"/>/<see cref="To"/> are inclusive ISO 8601 UTC bounds compared
/// lexicographically (valid because the stored format is fixed-width UTC).
/// </summary>
public record ScreeningQuery
{
    public string? CinemaId { get; init; }
    public int? MovieId { get; init; }
    public string? City { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public bool WithRelations { get; init; }
    public int Limit { get; init; } = 50_000;
    public int Offset { get; init; }
}
