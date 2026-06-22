using Felinoir.Domain.Entities;

namespace Felinoir.IntegrationTests.Support;

/// <summary>Factory helpers for valid entities used across the service tests.</summary>
internal static class TestData
{
    public static Movie Movie(
        int id,
        string title = "A Film",
        string? slug = null,
        string createdAt = "2026-01-01T00:00:00Z")
        => new()
        {
            Id = id,
            Title = title,
            Slug = slug,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

    public static Cinema Cinema(
        string id,
        string name = "A Cinema",
        string city = "Roma",
        string createdAt = "2026-01-01T00:00:00Z")
        => new()
        {
            Id = id,
            Name = name,
            City = city,
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
