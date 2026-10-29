namespace Felinoir.Domain.Entities;

/// <summary>
/// A single showing of a <see cref="Movie"/> at a <see cref="Cinema"/>.
/// Uniqueness is enforced on (CinemaId, MovieId, Datetime).
/// </summary>
public class Screening
{
    public int Id { get; init; }
    public int MovieId { get; init; }
    public string CinemaId { get; init; } = default!;

    /// <summary>
    /// Italian wall-clock time with no offset, stored as TEXT, e.g. "2026-06-22T20:30:00"; read it
    /// with <see cref="ValueObjects.ScreeningTime"/>.
    /// </summary>
    public string Datetime { get; init; } = default!;

    public string? Hall { get; init; }
    public bool Is3D { get; init; }
    public bool IsOV { get; init; }
    public string? SubtitleLanguage { get; init; }
    public string? BookingUrl { get; init; }

    /// <summary>Arbitrary JSON blob from the scraper; stored/returned as a raw string.</summary>
    public string? Meta { get; init; }

    public string CreatedAt { get; init; } = default!;
    public string UpdatedAt { get; init; } = default!;

    public Movie? Movie { get; init; }
    public Cinema? Cinema { get; init; }
}
