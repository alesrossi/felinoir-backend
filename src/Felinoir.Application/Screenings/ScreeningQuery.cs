namespace Felinoir.Application.Screenings;

/// <summary>
/// Filter/pagination criteria for <see cref="IScreeningService.GetAsync"/>.
/// <see cref="From"/>/<see cref="To"/> are inclusive bounds in Italian time; one with <c>Z</c> or an
/// offset is converted first. <see cref="City"/> is a city id or display name, ignoring case.
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
