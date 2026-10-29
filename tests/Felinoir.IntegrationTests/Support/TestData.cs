using Felinoir.Domain.Entities;

namespace Felinoir.IntegrationTests.Support;

/// <summary>Factory helpers for valid entities used across the service tests.</summary>
internal static class TestData
{
    /// <remarks>
    /// The default title is unique per id: the scrapers' dedup index (idx_movies_unique) rejects
    /// two movies with the same title and original title, exactly as production does.
    /// </remarks>
    public static Movie Movie(
        int id,
        string? title = null,
        string? slug = null,
        string createdAt = "2026-01-01T00:00:00Z")
        => new()
        {
            Id = id,
            Title = title ?? $"Film {id}",
            Slug = slug,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

    /// <summary>A city other than Rome, which tests add themselves (only Rome is seeded).</summary>
    public static City City(string id, string name) => new() { Id = id, Name = name };

    public static Cinema Cinema(
        string id,
        string name = "A Cinema",
        string cityId = Domain.Entities.City.RomeId,
        string createdAt = "2026-01-01T00:00:00Z")
        => new()
        {
            Id = id,
            Name = name,
            CityId = cityId,
            Country = "IT",
            Website = $"https://{id}.example.com",
            ScheduleUrl = $"https://{id}.example.com/schedule",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

    public static Screening Screening(
        int id,
        int movieId,
        string cinemaId,
        string datetime,
        string createdAt = "2026-01-01T00:00:00Z")
        => new()
        {
            Id = id,
            MovieId = movieId,
            CinemaId = cinemaId,
            Datetime = datetime,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
}
