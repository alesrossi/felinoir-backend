namespace Felinoir.Domain.Entities;

/// <summary>
/// A single showing of a <see cref="Movie"/> at a <see cref="Cinema"/>.
/// Uniqueness is enforced on (CinemaId, MovieId, Datetime).
/// </summary>
public class Screening
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string CinemaId { get; set; } = default!;

    /// <summary>
    /// Italian wall-clock time with no offset, stored as TEXT, e.g. "2026-06-22T20:30:00"; read it
    /// with <see cref="ValueObjects.ScreeningTime"/>.
    /// </summary>
    public string Datetime { get; set; } = default!;

    public string? Hall { get; set; }
    public bool Is3D { get; set; }
    public bool IsOV { get; set; }
    public string? SubtitleLanguage { get; set; }
    public string? BookingUrl { get; set; }

    /// <summary>Arbitrary JSON blob from the scraper; stored/returned as a raw string.</summary>
    public string? Meta { get; set; }

    public string CreatedAt { get; set; } = default!;
    public string UpdatedAt { get; set; } = default!;

    public Movie? Movie { get; set; }
    public Cinema? Cinema { get; set; }
}
