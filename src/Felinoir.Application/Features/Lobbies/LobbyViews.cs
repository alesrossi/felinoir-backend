using Felinoir.Application.Common;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Lobbies;
using Felinoir.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>Builds the <see cref="LobbyView"/> a device sees, after any change to the lobby or on a poll.</summary>
public sealed class LobbyViews(IReadDbContext db)
{
    /// <summary>The lobby as <paramref name="viewer"/> sees it; checks whether its screening is still scheduled.</summary>
    public async Task<LobbyView> ForAsync(Lobby lobby, DeviceHash viewer, CancellationToken cancellationToken)
    {
        var available = lobby.Screening.ScreeningId is { } screeningId
            && await db.Screenings.AnyAsync(s => s.Id == screeningId, cancellationToken);

        return Of(lobby, viewer, available);
    }

    public static LobbyView Of(Lobby lobby, DeviceHash viewer, bool available)
    {
        var participants = lobby.Participants
            .OrderBy(p => p.JoinedAt)
            .ThenBy(p => p.Id.Value)
            .Select(p => new LobbyParticipantView
            {
                Id = p.Id.Value,
                Name = p.Name.Value,
                JoinedAt = UtcText.Format(p.JoinedAt),
                IsMe = p.IsHeldBy(viewer),
            })
            .ToList();

        var screening = lobby.Screening;
        return new LobbyView
        {
            Id = lobby.Id.Value,
            Version = lobby.Version,
            Screening = new LobbyScreeningView
            {
                ScreeningId = available ? screening.ScreeningId : null,
                Available = available,
                MovieId = screening.MovieId,
                MovieSlug = screening.MovieSlug,
                MovieTitle = screening.MovieTitle,
                MovieTitleEn = screening.MovieTitleEn,
                PosterUrl = screening.PosterUrl,
                CinemaId = screening.CinemaId,
                CinemaName = screening.CinemaName,
                Datetime = ScreeningTime.FromInstant(lobby.StartsAt).ToString(),
                IsOV = screening.IsOV,
                Is3D = screening.Is3D,
                BookingUrl = screening.BookingUrl,
            },
            Note = lobby.Note.Value,
            Participants = participants,
            MeId = participants.FirstOrDefault(p => p.IsMe)?.Id,
            ExpiresAt = UtcText.Format(lobby.ExpiresAt),
        };
    }
}
