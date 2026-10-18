using System.Text.Json.Serialization;

namespace Felinoir.Application.Lobbies;

// Response shapes of the /lobbies endpoints. Unlike the rest of the API, nullable fields are
// always serialized (as null) rather than omitted: the frontend checks e.g. meId === null.

/// <summary>A lobby as seen by one device (<see cref="MeId"/> and each participant's <c>isMe</c> depend on it).</summary>
public sealed record LobbyView
{
    public required Guid Id { get; init; }
    public required long Version { get; init; }
    public required LobbyScreeningView Screening { get; init; }
    public required string Note { get; init; }

    /// <summary>Sorted by <see cref="LobbyParticipantView.JoinedAt"/>.</summary>
    public required IReadOnlyList<LobbyParticipantView> Participants { get; init; }

    /// <summary>The caller's own entry, or null when this device isn't on the list.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required Guid? MeId { get; init; }

    /// <summary>ISO 8601 UTC, 48 hours after the screening starts.</summary>
    public required string ExpiresAt { get; init; }
}

/// <summary>The screening snapshot taken when the lobby was created.</summary>
public sealed record LobbyScreeningView
{
    /// <summary>Null once the screening is no longer in the schedule.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? ScreeningId { get; init; }

    /// <summary>Whether the screening is still in the schedule.</summary>
    public required bool Available { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required int? MovieId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? MovieSlug { get; init; }

    public required string MovieTitle { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? MovieTitleEn { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? PosterUrl { get; init; }

    public required string CinemaId { get; init; }
    public required string CinemaName { get; init; }

    /// <summary>Rome wall clock without offset, same format as <c>screenings.datetime</c>.</summary>
    public required string Datetime { get; init; }

    public required bool IsOV { get; init; }
    public required bool Is3D { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? BookingUrl { get; init; }
}

public sealed record LobbyParticipantView
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    /// <summary>ISO 8601 UTC.</summary>
    public required string JoinedAt { get; init; }

    public required bool IsMe { get; init; }
}
