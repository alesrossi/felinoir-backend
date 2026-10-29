using Felinoir.Application.Lobbies;
using Felinoir.Domain.Entities;
using Felinoir.Infrastructure.Persistence;
using Felinoir.UnitTests.TestSupport;

namespace Felinoir.UnitTests.Lobbies;

/// <summary>
/// Service rules over the InMemory provider. Database-only behaviour (version triggers,
/// unique indexes, the cleanup function) is covered by the integration tests.
/// </summary>
public sealed class LobbyServiceTests : IDisposable
{
    // 2026-10-16 18:00 in Rome (CEST).
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 16, 0, 0, TimeSpan.Zero);

    private const string Me = "my-device-token";
    private const string Friend = "friend-device-token";

    private readonly ApplicationDbContext _db = InMemoryDb.Create();
    private readonly FixedTimeProvider _time = new(Now);

    public LobbyServiceTests()
    {
        _db.Movies.Add(new Movie
        {
            Id = 42, Title = "Dune - Parte due", TmdbTitle = "Dune - Parte due (TMDB)", TmdbTitleEn = "Dune: Part Two",
            Slug = "dune-parte-due", PosterUrl = "https://scraped/poster.jpg", TmdbPosterUrl = "https://tmdb/poster.jpg",
            CreatedAt = "2026-01-01T00:00:00Z", UpdatedAt = "2026-01-01T00:00:00Z",
        });
        _db.Cinemas.Add(new Cinema
        {
            Id = "nuovo-sacher", Name = "Nuovo Sacher", CityId = City.RomeId, Country = "IT",
            Website = "https://sacher", ScheduleUrl = "https://sacher/schedule",
            CreatedAt = "2026-01-01T00:00:00Z", UpdatedAt = "2026-01-01T00:00:00Z",
        });
        AddScreening(12345, "2026-10-17T21:00:00");
        AddScreening(999, "2026-10-16T17:59:00");   // started a minute ago
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void AddScreening(int id, string datetime) => _db.Screenings.Add(new Screening
    {
        Id = id, MovieId = 42, CinemaId = "nuovo-sacher", Datetime = datetime, IsOV = true,
        BookingUrl = "https://sacher/book", CreatedAt = "2026-01-01T00:00:00Z", UpdatedAt = "2026-01-01T00:00:00Z",
    });

    private LobbyService NewService() => new(_db, _time);

    private async Task<LobbyView> CreateLobbyAsync(string name = "Giulia")
    {
        var result = await NewService().CreateAsync(12345, name, "Ci vediamo all'ingresso", Me);
        return result.Lobby!;
    }

    [Fact]
    public async Task Create_snapshots_the_screening_from_the_schedule()
    {
        var lobby = await CreateLobbyAsync();

        var s = lobby.Screening;
        Assert.Equal(12345, s.ScreeningId);
        Assert.True(s.Available);
        Assert.Equal(42, s.MovieId);
        Assert.Equal("dune-parte-due", s.MovieSlug);
        Assert.Equal("Dune - Parte due (TMDB)", s.MovieTitle);
        Assert.Equal("Dune: Part Two", s.MovieTitleEn);
        Assert.Equal("https://tmdb/poster.jpg", s.PosterUrl);
        Assert.Equal("nuovo-sacher", s.CinemaId);
        Assert.Equal("Nuovo Sacher", s.CinemaName);
        Assert.Equal("2026-10-17T21:00:00", s.Datetime);
        Assert.True(s.IsOV);
        Assert.False(s.Is3D);
        Assert.Equal("https://sacher/book", s.BookingUrl);

        Assert.Equal("Ci vediamo all'ingresso", lobby.Note);
        // 21:00 CEST is 19:00 UTC; plus 48 hours.
        Assert.Equal("2026-10-19T19:00:00Z", lobby.ExpiresAt);
    }

    [Fact]
    public async Task Create_adds_the_creator_as_my_participant()
    {
        var lobby = await CreateLobbyAsync("  Giulia ");

        var p = Assert.Single(lobby.Participants);
        Assert.Equal("Giulia", p.Name);
        Assert.True(p.IsMe);
        Assert.Equal("2026-10-16T16:00:00Z", p.JoinedAt);
        Assert.Equal(p.Id, lobby.MeId);
    }

    [Fact]
    public async Task Create_defaults_the_note_to_empty()
    {
        var result = await NewService().CreateAsync(12345, "Giulia", null, Me);

        Assert.Equal("", result.Lobby!.Note);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(777)]
    public async Task Create_rejects_unknown_screenings(int? screeningId)
    {
        var result = await NewService().CreateAsync(screeningId, "Giulia", null, Me);

        Assert.Equal(LobbyError.ScreeningNotFound, result.Error);
    }

    [Fact]
    public async Task Create_rejects_screenings_that_already_started()
    {
        var result = await NewService().CreateAsync(999, "Giulia", null, Me);

        Assert.Equal(LobbyError.ScreeningPast, result.Error);
    }

    [Fact]
    public async Task Create_validates_name_and_note()
    {
        var service = NewService();

        Assert.Equal(LobbyError.InvalidName, (await service.CreateAsync(12345, " ", null, Me)).Error);
        Assert.Equal(LobbyError.InvalidNote, (await service.CreateAsync(12345, "Giulia", new string('a', 501), Me)).Error);
    }

    [Fact]
    public async Task Get_shows_another_device_that_it_is_not_on_the_list()
    {
        var created = await CreateLobbyAsync();

        var lobby = (await NewService().GetAsync(created.Id, Friend)).Lobby!;

        Assert.Null(lobby.MeId);
        Assert.False(Assert.Single(lobby.Participants).IsMe);
    }

    [Fact]
    public async Task Get_with_the_current_version_is_not_modified()
    {
        var created = await CreateLobbyAsync();
        var service = NewService();

        Assert.True((await service.GetAsync(created.Id, Me, ifVersion: created.Version)).NotModified);
        Assert.NotNull((await service.GetAsync(created.Id, Me, ifVersion: created.Version + 1)).Lobby);
    }

    [Fact]
    public async Task Get_of_unknown_lobby_is_not_found()
    {
        var result = await NewService().GetAsync(Guid.NewGuid(), Me);

        Assert.Equal(LobbyError.NotFound, result.Error);
    }

    [Fact]
    public async Task Lobby_expires_48_hours_after_the_screening_starts()
    {
        var created = await CreateLobbyAsync();
        var service = NewService();

        _time.Now = new DateTimeOffset(2026, 10, 19, 19, 0, 0, TimeSpan.Zero);
        Assert.NotNull((await service.GetAsync(created.Id, Me)).Lobby);

        _time.Now = _time.Now.AddSeconds(1);
        Assert.Equal(LobbyError.NotFound, (await service.GetAsync(created.Id, Me)).Error);
        Assert.Equal(LobbyError.NotFound, (await service.JoinAsync(created.Id, "Marco", Friend)).Error);
        Assert.Equal(LobbyError.NotFound, (await service.UpdateNoteAsync(created.Id, "x", Me)).Error);
    }

    [Fact]
    public async Task Lobby_stays_usable_after_the_screening_starts()
    {
        var created = await CreateLobbyAsync();
        _time.Now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

        var result = await NewService().JoinAsync(created.Id, "Marco", Friend);

        Assert.NotNull(result.Lobby);
    }

    [Fact]
    public async Task Get_reports_a_screening_that_left_the_schedule()
    {
        var created = await CreateLobbyAsync();
        _db.Screenings.Remove(_db.Screenings.Single(s => s.Id == 12345));
        await _db.SaveChangesAsync();

        var lobby = (await NewService().GetAsync(created.Id, Me)).Lobby!;

        Assert.False(lobby.Screening.Available);
        Assert.Null(lobby.Screening.ScreeningId);
        Assert.Equal("Dune - Parte due (TMDB)", lobby.Screening.MovieTitle);
    }

    [Fact]
    public async Task UpdateNote_changes_the_note_and_validates_it()
    {
        var created = await CreateLobbyAsync();
        var service = NewService();

        Assert.Equal("Alle 20:45", (await service.UpdateNoteAsync(created.Id, "Alle 20:45", Friend)).Lobby!.Note);
        Assert.Equal(LobbyError.InvalidNote, (await service.UpdateNoteAsync(created.Id, null, Me)).Error);
        Assert.Equal(LobbyError.InvalidNote, (await service.UpdateNoteAsync(created.Id, new string('a', 501), Me)).Error);
    }

    [Fact]
    public async Task Join_adds_the_device_sorted_by_join_time()
    {
        var created = await CreateLobbyAsync();
        _time.Now = Now.AddMinutes(3);

        var lobby = (await NewService().JoinAsync(created.Id, "Marco", Friend)).Lobby!;

        Assert.Equal(["Giulia", "Marco"], lobby.Participants.Select(p => p.Name));
        Assert.Equal(lobby.Participants[1].Id, lobby.MeId);
        Assert.Equal([false, true], lobby.Participants.Select(p => p.IsMe));
    }

    [Fact]
    public async Task Join_rejects_a_device_that_already_joined()
    {
        var created = await CreateLobbyAsync();

        var result = await NewService().JoinAsync(created.Id, "Marco", Me);

        Assert.Equal(LobbyError.AlreadyJoined, result.Error);
    }

    [Fact]
    public async Task Join_rejects_a_taken_name_case_insensitively()
    {
        var created = await CreateLobbyAsync();

        var result = await NewService().JoinAsync(created.Id, " GIULIA ", Friend);

        Assert.Equal(LobbyError.NameTaken, result.Error);
    }

    [Fact]
    public async Task Join_validates_the_name()
    {
        var created = await CreateLobbyAsync();

        Assert.Equal(LobbyError.InvalidName, (await NewService().JoinAsync(created.Id, "a\u0000b", Friend)).Error);
    }

    [Fact]
    public async Task Rename_works_for_anyone_and_allows_recasing_your_own_name()
    {
        var created = await CreateLobbyAsync();
        var giulia = created.Participants[0].Id;
        var service = NewService();

        Assert.Equal("GIULIA", (await service.RenameParticipantAsync(created.Id, giulia, "GIULIA", Me)).Lobby!.Participants[0].Name);
        Assert.Equal("Giuly", (await service.RenameParticipantAsync(created.Id, giulia, "Giuly", Friend)).Lobby!.Participants[0].Name);
    }

    [Fact]
    public async Task Rename_rejects_another_participants_name()
    {
        var created = await CreateLobbyAsync();
        var marco = (await NewService().JoinAsync(created.Id, "Marco", Friend)).Lobby!.MeId!.Value;

        var result = await NewService().RenameParticipantAsync(created.Id, marco, "giulia", Friend);

        Assert.Equal(LobbyError.NameTaken, result.Error);
    }

    [Fact]
    public async Task Rename_and_remove_of_unknown_participant_are_not_found()
    {
        var created = await CreateLobbyAsync();
        var service = NewService();

        Assert.Equal(LobbyError.NotFound, (await service.RenameParticipantAsync(created.Id, Guid.NewGuid(), "X", Me)).Error);
        Assert.Equal(LobbyError.NotFound, (await service.RemoveParticipantAsync(created.Id, Guid.NewGuid(), Me)).Error);
        Assert.Equal(LobbyError.NotFound, (await service.ClaimParticipantAsync(created.Id, Guid.NewGuid(), Me)).Error);
    }

    [Fact]
    public async Task Remove_takes_anyone_off_the_list()
    {
        var created = await CreateLobbyAsync();

        var lobby = (await NewService().RemoveParticipantAsync(created.Id, created.MeId!.Value, Me)).Lobby!;

        Assert.Empty(lobby.Participants);
        Assert.Null(lobby.MeId);
    }

    [Fact]
    public async Task Claim_moves_this_device_onto_the_entry()
    {
        var created = await CreateLobbyAsync();
        var service = NewService();
        var marco = (await service.JoinAsync(created.Id, "Marco", Friend)).Lobby!.MeId!.Value;

        // My device switches from "Giulia" to "Marco"; the friend's device loses "Marco".
        var mine = (await service.ClaimParticipantAsync(created.Id, marco, Me)).Lobby!;
        Assert.Equal(marco, mine.MeId);
        Assert.Equal(1, mine.Participants.Count(p => p.IsMe));

        var theirs = (await service.GetAsync(created.Id, Friend)).Lobby!;
        Assert.Null(theirs.MeId);
    }

    [Fact]
    public async Task Claim_of_my_own_entry_changes_nothing()
    {
        var created = await CreateLobbyAsync();

        var lobby = (await NewService().ClaimParticipantAsync(created.Id, created.MeId!.Value, Me)).Lobby!;

        Assert.Equal(created.MeId, lobby.MeId);
    }
}
