using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felinoir.IntegrationTests.Persistence;

/// <summary>
/// Checks that the EF mapping of <c>lobbies</c> / <c>lobby_participants</c> works against
/// the scrapers-owned DDL: database defaults, the version-bumping triggers, the
/// constraints the API maps to error codes, and the cleanup function.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LobbySchemaTests : PostgresTestBase
{
    public LobbySchemaTests(PostgresFixture fixture) : base(fixture) { }

    private static Lobby NewLobby(DateTimeOffset? startsAt = null) => new()
    {
        MovieTitle = "Il Sorpasso",
        CinemaId = "farnese",
        CinemaName = "Farnese",
        StartsAt = startsAt ?? DateTimeOffset.UtcNow.AddDays(1),
    };

    [Fact]
    public async Task Insert_fills_database_defaults()
    {
        await using var db = Fixture.NewDbContext();
        var lobby = NewLobby();
        db.Lobbies.Add(lobby);
        await db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, lobby.Id);
        Assert.Equal(1, lobby.Version);
        Assert.Equal("", lobby.Note);
        Assert.True(lobby.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Participant_changes_and_note_changes_bump_version()
    {
        await using var db = Fixture.NewDbContext();
        var lobby = NewLobby();
        db.Lobbies.Add(lobby);
        await db.SaveChangesAsync();

        db.LobbyParticipants.Add(new LobbyParticipant { LobbyId = lobby.Id, Name = "Anna" });
        await db.SaveChangesAsync();

        lobby.Note = "Ci vediamo all'ingresso";
        await db.SaveChangesAsync();

        // Participant insert (+1) and note change (+1), read back by EF after the update.
        Assert.Equal(3, lobby.Version);
    }

    [Fact]
    public async Task Names_are_unique_per_lobby_case_insensitively()
    {
        await using var db = Fixture.NewDbContext();
        var lobby = NewLobby();
        db.Lobbies.Add(lobby);
        db.LobbyParticipants.Add(new LobbyParticipant { Lobby = lobby, Name = "Anna" });
        await db.SaveChangesAsync();

        await using var db2 = Fixture.NewDbContext();
        db2.LobbyParticipants.Add(new LobbyParticipant { LobbyId = lobby.Id, Name = "anna" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("lobby_participants_name_uniq", pg.ConstraintName);
    }

    [Fact]
    public async Task A_device_holds_one_name_per_lobby_but_unclaimed_names_are_unlimited()
    {
        var hash = new byte[32];
        await using var db = Fixture.NewDbContext();
        var lobby = NewLobby();
        db.Lobbies.Add(lobby);
        db.LobbyParticipants.AddRange(
            new LobbyParticipant { Lobby = lobby, Name = "Anna" },
            new LobbyParticipant { Lobby = lobby, Name = "Bruno" },
            new LobbyParticipant { Lobby = lobby, Name = "Carla", DeviceHash = hash });
        await db.SaveChangesAsync();

        await using var db2 = Fixture.NewDbContext();
        db2.LobbyParticipants.Add(new LobbyParticipant { LobbyId = lobby.Id, Name = "Dario", DeviceHash = hash });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal("lobby_participants_one_per_device", pg.ConstraintName);
    }

    [Fact]
    public async Task Delete_expired_lobbies_removes_only_lobbies_older_than_48_hours()
    {
        await using var db = Fixture.NewDbContext();
        var expired = NewLobby(DateTimeOffset.UtcNow.AddHours(-49));
        var recent = NewLobby(DateTimeOffset.UtcNow.AddHours(-47));
        db.Lobbies.AddRange(expired, recent);
        db.LobbyParticipants.Add(new LobbyParticipant { Lobby = expired, Name = "Anna" });
        await db.SaveChangesAsync();

        var deleted = await db.Database
            .SqlQuery<int>($"SELECT delete_expired_lobbies() AS \"Value\"")
            .SingleAsync();

        Assert.Equal(1, deleted);
        Assert.Equal([recent.Id], await db.Lobbies.AsNoTracking().Select(l => l.Id).ToListAsync());
        Assert.Empty(await db.LobbyParticipants.AsNoTracking().ToListAsync());
    }
}
