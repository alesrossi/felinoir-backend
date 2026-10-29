namespace Felinoir.Domain.Lobbies;

/// <summary>
/// The copy of a screening's film and cinema a <see cref="Lobby"/> takes when it is created
/// (see CONTEXT.md). It never changes afterwards: screenings are re-scraped and their rows come and
/// go, so a lobby must keep rendering without them. <see cref="ScreeningId"/> is only used to tell
/// whether the screening is still in the schedule. Its start is the lobby's <see cref="Lobby.StartsAt"/>.
/// </summary>
public sealed record ScreeningSnapshot
{
    public int? ScreeningId { get; init; }
    public int? MovieId { get; init; }
    public string? MovieSlug { get; init; }
    public required string MovieTitle { get; init; }
    public string? MovieTitleEn { get; init; }
    public string? PosterUrl { get; init; }
    public required string CinemaId { get; init; }
    public required string CinemaName { get; init; }

    public bool IsOV { get; init; }
    public bool Is3D { get; init; }
    public string? BookingUrl { get; init; }
}
