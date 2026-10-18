using Felinoir.Application.Common.Exceptions;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Lobbies;

public class LobbyService : ILobbyService
{
    /// <summary>How long after its screening starts a lobby stays reachable.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    // Unique constraints on lobby_participants (owned by the scrapers' schema). The in-memory
    // checks below give the usual answer; these catch the same conflict from a concurrent request.
    private const string NameUniqueConstraint = "lobby_participants_name_uniq";
    private const string DeviceUniqueConstraint = "lobby_participants_one_per_device";

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _time;

    public LobbyService(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<LobbyResult> CreateAsync(
        int? screeningId, string? name, string? note, string deviceToken, CancellationToken ct = default)
    {
        if (LobbyInput.NormalizeName(name) is not { } validName) return LobbyError.InvalidName;
        note ??= "";
        if (!LobbyInput.IsValidNote(note)) return LobbyError.InvalidNote;

        // The snapshot comes from our own screening data, never from the client.
        var screening = screeningId is null
            ? null
            : await _db.Screenings.AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Cinema)
                .FirstOrDefaultAsync(s => s.Id == screeningId, ct);

        if (screening is null || !RomeTime.TryParseWallClock(screening.Datetime, out var startsAt))
            return LobbyError.ScreeningNotFound;

        var now = _time.GetUtcNow();
        if (startsAt <= now) return LobbyError.ScreeningPast;

        var hash = LobbyInput.HashDeviceToken(deviceToken);
        var movie = screening.Movie!;
        var lobby = new Lobby
        {
            ScreeningId = screening.Id,
            MovieId = movie.Id,
            MovieSlug = movie.Slug,
            MovieTitle = movie.TmdbTitle ?? movie.Title,
            MovieTitleEn = movie.TmdbTitleEn,
            PosterUrl = movie.TmdbPosterUrl ?? movie.PosterUrl,
            CinemaId = screening.CinemaId,
            CinemaName = screening.Cinema?.Name ?? screening.CinemaId,
            StartsAt = startsAt,
            IsOV = screening.IsOV,
            Is3D = screening.Is3D,
            BookingUrl = screening.BookingUrl,
            Note = note,
            CreatedAt = now,
            Participants = [new LobbyParticipant { Name = validName, DeviceHash = hash, JoinedAt = now }],
        };

        _db.Lobbies.Add(lobby);
        await _db.SaveChangesAsync(ct);

        return LobbyResult.Ok((await LoadViewAsync(lobby.Id, hash, ct))!);
    }

    public async Task<LobbyResult> GetAsync(
        Guid id, string deviceToken, long? ifVersion = null, CancellationToken ct = default)
    {
        // Polling hits this once a second per open page: a primary-key lookup of two columns
        // decides the common "nothing changed" answer.
        var head = await _db.Lobbies.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new { l.Version, l.StartsAt })
            .FirstOrDefaultAsync(ct);

        if (head is null || IsExpired(head.StartsAt)) return LobbyError.NotFound;
        if (head.Version == ifVersion) return LobbyResult.Unchanged;

        var view = await LoadViewAsync(id, LobbyInput.HashDeviceToken(deviceToken), ct);
        return view is null ? LobbyError.NotFound : LobbyResult.Ok(view);
    }

    public async Task<LobbyResult> UpdateNoteAsync(
        Guid id, string? note, string deviceToken, CancellationToken ct = default)
    {
        if (note is null || !LobbyInput.IsValidNote(note)) return LobbyError.InvalidNote;

        var lobby = await LoadActiveAsync(id, ct);
        if (lobby is null) return LobbyError.NotFound;

        lobby.Note = note;
        return await SaveAndViewAsync(id, deviceToken, ct);
    }

    public async Task<LobbyResult> JoinAsync(
        Guid id, string? name, string deviceToken, CancellationToken ct = default)
    {
        if (LobbyInput.NormalizeName(name) is not { } validName) return LobbyError.InvalidName;

        var lobby = await LoadActiveAsync(id, ct);
        if (lobby is null) return LobbyError.NotFound;

        var hash = LobbyInput.HashDeviceToken(deviceToken);
        if (lobby.Participants.Any(p => IsDevice(p, hash))) return LobbyError.AlreadyJoined;
        if (lobby.Participants.Any(p => SameName(p.Name, validName))) return LobbyError.NameTaken;

        _db.LobbyParticipants.Add(new LobbyParticipant
        {
            LobbyId = lobby.Id,
            Name = validName,
            DeviceHash = hash,
            JoinedAt = _time.GetUtcNow(),
        });
        return await SaveAndViewAsync(id, deviceToken, ct);
    }

    public async Task<LobbyResult> RenameParticipantAsync(
        Guid id, Guid participantId, string? name, string deviceToken, CancellationToken ct = default)
    {
        if (LobbyInput.NormalizeName(name) is not { } validName) return LobbyError.InvalidName;

        var lobby = await LoadActiveAsync(id, ct);
        var participant = lobby?.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return LobbyError.NotFound;

        if (lobby!.Participants.Any(p => p.Id != participantId && SameName(p.Name, validName)))
            return LobbyError.NameTaken;

        participant.Name = validName;
        return await SaveAndViewAsync(id, deviceToken, ct);
    }

    public async Task<LobbyResult> RemoveParticipantAsync(
        Guid id, Guid participantId, string deviceToken, CancellationToken ct = default)
    {
        var lobby = await LoadActiveAsync(id, ct);
        var participant = lobby?.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return LobbyError.NotFound;

        _db.LobbyParticipants.Remove(participant);
        return await SaveAndViewAsync(id, deviceToken, ct);
    }

    public async Task<LobbyResult> ClaimParticipantAsync(
        Guid id, Guid participantId, string deviceToken, CancellationToken ct = default)
    {
        var lobby = await LoadActiveAsync(id, ct);
        var participant = lobby?.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return LobbyError.NotFound;

        var hash = LobbyInput.HashDeviceToken(deviceToken);
        if (IsDevice(participant, hash)) return await SaveAndViewAsync(id, deviceToken, ct);

        // One device per lobby is a unique constraint checked per statement, so the device
        // must leave its old entry (first save) before it takes the new one (second save).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var previous in lobby!.Participants.Where(p => IsDevice(p, hash)))
            previous.DeviceHash = null;
        await _db.SaveChangesAsync(ct);

        participant.DeviceHash = hash;
        var result = await SaveAndViewAsync(id, deviceToken, ct);

        if (result.Error is null) await tx.CommitAsync(ct);
        return result;
    }

    /// <summary>The lobby with its participants, tracked for changes; null if missing or expired.</summary>
    private async Task<Lobby?> LoadActiveAsync(Guid id, CancellationToken ct)
    {
        var lobby = await _db.Lobbies
            .Include(l => l.Participants)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        return lobby is null || IsExpired(lobby.StartsAt) ? null : lobby;
    }

    /// <summary>Saves pending changes and returns the lobby re-read with its trigger-bumped version.</summary>
    private async Task<LobbyResult> SaveAndViewAsync(Guid id, string deviceToken, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == NameUniqueConstraint)
        {
            return LobbyError.NameTaken;
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == DeviceUniqueConstraint)
        {
            return LobbyError.AlreadyJoined;
        }

        var view = await LoadViewAsync(id, LobbyInput.HashDeviceToken(deviceToken), ct);
        return view is null ? LobbyError.NotFound : LobbyResult.Ok(view);
    }

    private async Task<LobbyView?> LoadViewAsync(Guid id, byte[] deviceHash, CancellationToken ct)
    {
        var lobby = await _db.Lobbies.AsNoTracking()
            .Include(l => l.Participants)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lobby is null) return null;

        var available = lobby.ScreeningId is { } screeningId
            && await _db.Screenings.AnyAsync(s => s.Id == screeningId, ct);

        var participants = lobby.Participants
            .OrderBy(p => p.JoinedAt)
            .ThenBy(p => p.Id)
            .Select(p => new LobbyParticipantView
            {
                Id = p.Id,
                Name = p.Name,
                JoinedAt = RomeTime.ToUtcString(p.JoinedAt),
                IsMe = IsDevice(p, deviceHash),
            })
            .ToList();

        return new LobbyView
        {
            Id = lobby.Id,
            Version = lobby.Version,
            Screening = new LobbyScreeningView
            {
                ScreeningId = available ? lobby.ScreeningId : null,
                Available = available,
                MovieId = lobby.MovieId,
                MovieSlug = lobby.MovieSlug,
                MovieTitle = lobby.MovieTitle,
                MovieTitleEn = lobby.MovieTitleEn,
                PosterUrl = lobby.PosterUrl,
                CinemaId = lobby.CinemaId,
                CinemaName = lobby.CinemaName,
                Datetime = RomeTime.ToWallClock(lobby.StartsAt),
                IsOV = lobby.IsOV,
                Is3D = lobby.Is3D,
                BookingUrl = lobby.BookingUrl,
            },
            Note = lobby.Note,
            Participants = participants,
            MeId = participants.FirstOrDefault(p => p.IsMe)?.Id,
            ExpiresAt = RomeTime.ToUtcString(lobby.StartsAt + Lifetime),
        };
    }

    // Same condition as delete_expired_lobbies(), which may not have run yet.
    private bool IsExpired(DateTimeOffset startsAt) => startsAt + Lifetime < _time.GetUtcNow();

    private static bool IsDevice(LobbyParticipant p, byte[] deviceHash) =>
        p.DeviceHash is not null && p.DeviceHash.AsSpan().SequenceEqual(deviceHash);

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
