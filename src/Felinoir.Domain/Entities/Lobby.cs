namespace Felinoir.Domain.Entities;

/// <summary>
/// A group outing to one screening. The screening's details are copied in once at
/// creation (no FK to <see cref="Screening"/>: screenings are re-scraped and their ids churn),
/// so a lobby keeps rendering even after its screening row is gone.
/// Expired lobbies (<see cref="StartsAt"/> more than 48 hours ago) are deleted by
/// <c>delete_expired_lobbies()</c>; until that runs they must be treated as not found.
/// </summary>
public class Lobby
{
    public Guid Id { get; set; }

    /// <summary>
    /// Bumped by database triggers on every note change and every participant
    /// insert/update/delete; clients poll it. Never written by this server.
    /// </summary>
    public long Version { get; set; }

    public int? ScreeningId { get; set; }
    public int? MovieId { get; set; }
    public string? MovieSlug { get; set; }
    public string MovieTitle { get; set; } = default!;
    public string? MovieTitleEn { get; set; }
    public string? PosterUrl { get; set; }
    public string CinemaId { get; set; } = default!;
    public string CinemaName { get; set; } = default!;

    /// <summary>
    /// TIMESTAMPTZ, unlike the TEXT timestamps elsewhere. The screening's Rome wall-clock
    /// time must be converted with the Europe/Rome zone before storing, and back to Rome
    /// wall-clock when returned by the API.
    /// </summary>
    public DateTimeOffset StartsAt { get; set; }

    public bool IsOV { get; set; }
    public bool Is3D { get; set; }
    public string? BookingUrl { get; set; }

    /// <summary>At most 500 characters (CHECK).</summary>
    public string Note { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public List<LobbyParticipant> Participants { get; set; } = [];
}
