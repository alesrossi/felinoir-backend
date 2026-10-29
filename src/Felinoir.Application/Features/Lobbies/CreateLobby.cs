using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;
using Felinoir.Domain.ValueObjects;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>
/// Creates a lobby for a screening that hasn't started, with the calling device as its first
/// participant. The screening's details are copied from the schedule, never taken from the client.
/// </summary>
public sealed record CreateLobbyCommand(int? ScreeningId, string? Name, string? Note, string DeviceToken)
    : ICommand<Result<LobbyView>>;

public sealed class CreateLobbyValidator : AbstractValidator<CreateLobbyCommand>
{
    public CreateLobbyValidator()
    {
        RuleFor(c => c.DeviceToken).IsDeviceToken();
        RuleFor(c => c.Name).IsParticipantName();
        RuleFor(c => c.Note).IsLobbyNote().When(c => c.Note is not null);
    }
}

public sealed class CreateLobbyHandler(IReadDbContext db, ILobbyRepository lobbies, TimeProvider time)
    : ICommandHandler<CreateLobbyCommand, Result<LobbyView>>
{
    public async ValueTask<Result<LobbyView>> Handle(CreateLobbyCommand command, CancellationToken cancellationToken)
    {
        var screening = command.ScreeningId is not { } screeningId
            ? null
            : await db.Screenings
                .Where(s => s.Id == screeningId)
                .Select(s => new
                {
                    s.Datetime,
                    Snapshot = new ScreeningSnapshot
                    {
                        ScreeningId = s.Id,
                        MovieId = s.MovieId,
                        MovieSlug = s.Movie!.Slug,
                        MovieTitle = s.Movie.TmdbTitle ?? s.Movie.Title,
                        MovieTitleEn = s.Movie.TmdbTitleEn,
                        PosterUrl = s.Movie.TmdbPosterUrl ?? s.Movie.PosterUrl,
                        CinemaId = s.CinemaId,
                        CinemaName = s.Cinema!.Name,
                        IsOV = s.IsOV,
                        Is3D = s.Is3D,
                        BookingUrl = s.BookingUrl,
                    },
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (screening is null || !ScreeningTime.TryParse(screening.Datetime, out var startsAt))
            return LobbyErrors.ScreeningNotFound;

        // The validator has checked the name and the note.
        ParticipantName.TryCreate(command.Name, out var name);
        LobbyNote.TryCreate(command.Note ?? "", out var note);
        var device = DeviceHash.FromToken(command.DeviceToken);

        var created = Lobby.Create(screening.Snapshot, startsAt.ToInstant(), name, device, note, time.GetUtcNow());
        if (!created.IsSuccess) return created.Error;

        lobbies.Add(created.Value);
        return LobbyViews.Of(created.Value, device, available: true);
    }
}
