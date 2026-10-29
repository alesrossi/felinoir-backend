using Felinoir.Application.Common.Exceptions;
using Felinoir.Domain.Lobbies;
using Felinoir.Infrastructure.Persistence;
using Felinoir.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Felinoir.IntegrationTests.Persistence;

/// <summary>
/// The Lobby aggregate against real Postgres: its mapping, the version as concurrency token, the
/// deferred one-entry-per-device constraint that lets a claim swap devices in one save, the unique
/// backstops behind the aggregate's own checks, and the expiry function.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LobbyPersistenceTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static readonly DeviceHash Me = DeviceHash.FromToken("me");
    private static readonly DeviceHash Friend = DeviceHash.FromToken("friend");

    private static ParticipantName Name(string text)
    {
        ParticipantName.TryCreate(text, out var name);
        return name;
    }

    private static Lobby NewLobby(DateTimeOffset? startsAt = null, DateTimeOffset? now = null) =>
        Lobby.Create(
            new ScreeningSnapshot { MovieTitle = "Il Sorpasso", CinemaId = "farnese", CinemaName = "Farnese", IsOV = true },
            startsAt ?? Now.AddDays(1), Name("Ale"), Me, LobbyNote.Empty, now ?? Now.AddDays(-3)).Value;

    private async Task<LobbyId> SaveAsync(Lobby lobby)
    {
        await using var db = Fixture.NewDbContext();
        db.Lobbies.Add(lobby);
        await db.SaveChangesAsync();
        return lobby.Id;
    }

    private static async Task<Lobby> LoadAsync(ApplicationDbContext db, LobbyId id) =>
        (await new LobbyRepository(db).GetAsync(id))!;

    [Fact]
    public async Task A_lobby_round_trips_with_its_snapshot_and_participants()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);
        var id = await SaveAsync(lobby);

        await using var db = Fixture.NewDbContext();
        var loaded = await LoadAsync(db, id);

        Assert.Equal(2, loaded.Version);
        Assert.Equal("Il Sorpasso", loaded.Screening.MovieTitle);
        Assert.True(loaded.Screening.IsOV);
        Assert.Equal(lobby.StartsAt.ToUnixTimeSeconds(), loaded.StartsAt.ToUnixTimeSeconds());
        Assert.Equal(["Ale", "Giulia"], loaded.Participants.OrderBy(p => p.JoinedAt).Select(p => p.Name.Value));
        Assert.True(loaded.EntryOf(Friend)!.Name.SameAs(Name("giulia")));
    }

    [Fact]
    public async Task Changes_are_saved_with_the_new_version()
    {
        var id = await SaveAsync(NewLobby());

        await using (var db = Fixture.NewDbContext())
        {
            var lobby = await LoadAsync(db, id);
            LobbyNote.TryCreate("Ingresso", out var note);
            lobby.ChangeNote(note);
            lobby.Join(Name("Giulia"), Friend, Now);
            await db.SaveChangesAsync();
        }

        await using var check = Fixture.NewDbContext();
        var saved = await LoadAsync(check, id);
        Assert.Equal(3, saved.Version);
        Assert.Equal("Ingresso", saved.Note.Value);
        Assert.Equal(2, saved.Participants.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_claim_moves_a_device_between_entries_in_one_save_in_either_order(bool claimTheNewerEntry)
    {
        // The two UPDATEs run in key order, and ids are time-ordered: covering both directions
        // proves the one-per-device constraint is only checked at commit.
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);
        var id = await SaveAsync(lobby);

        await using (var db = Fixture.NewDbContext())
        {
            var loaded = await LoadAsync(db, id);
            var ale = loaded.EntryOf(Me)!;
            var giulia = loaded.EntryOf(Friend)!;
            var (claimer, target) = claimTheNewerEntry ? (Me, giulia) : (Friend, ale);
            Assert.True(loaded.Claim(target.Id, claimer).IsSuccess);
            await db.SaveChangesAsync();
        }

        await using var check = Fixture.NewDbContext();
        var saved = await LoadAsync(check, id);
        Assert.Single(saved.Participants, p => p.Device is not null);
        Assert.Equal(3, saved.Version);
    }

    [Fact]
    public async Task Of_two_concurrent_changes_the_second_save_loses()
    {
        var id = await SaveAsync(NewLobby());

        await using var first = Fixture.NewDbContext();
        await using var second = Fixture.NewDbContext();
        var a = await LoadAsync(first, id);
        var b = await LoadAsync(second, id);

        a.Join(Name("Giulia"), Friend, Now);
        await first.SaveChangesAsync();

        b.Join(Name("Marco"), DeviceHash.FromToken("third"), Now);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());

        second.DiscardChanges();
        Assert.Equal(["Ale", "Giulia"],
            (await LoadAsync(second, id)).Participants.OrderBy(p => p.JoinedAt).Select(p => p.Name.Value));
    }

    [Fact]
    public async Task Names_are_unique_per_lobby_ignoring_case_in_the_database_too()
    {
        var id = await SaveAsync(NewLobby());

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Sql($"INSERT INTO lobby_participants (lobby_id, name) VALUES ('{id}', 'ALE')"));

        Assert.Equal("lobby_participants_name_uniq", ex.ConstraintName);
    }

    [Fact]
    public async Task A_device_holds_one_entry_per_lobby_in_the_database_too_but_unclaimed_entries_are_unlimited()
    {
        var id = await SaveAsync(NewLobby());

        await Sql($"INSERT INTO lobby_participants (lobby_id, name) VALUES ('{id}', 'A'), ('{id}', 'B')");
        var hex = Convert.ToHexString(Me.ToArray());
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Sql($"INSERT INTO lobby_participants (lobby_id, name, device_hash) VALUES ('{id}', 'C', '\\x{hex}')"));

        Assert.Equal("lobby_participants_one_per_device", ex.ConstraintName);
    }

    [Fact]
    public async Task A_constraint_failing_at_commit_surfaces_as_a_unique_violation()
    {
        // Two contexts that never saw each other's change can't trip the version check on a new
        // lobby; the deferred device constraint is the backstop, and fails at commit.
        var id = await SaveAsync(NewLobby());
        await Sql($"INSERT INTO lobby_participants (lobby_id, name) VALUES ('{id}', 'Libero')");

        await using var db = Fixture.NewDbContext();
        var lobby = await LoadAsync(db, id);
        await Sql($"UPDATE lobby_participants SET device_hash = NULL WHERE lobby_id = '{id}'");
        await Sql($"UPDATE lobby_participants SET device_hash = '\\x{Convert.ToHexString(Friend.ToArray())}' WHERE lobby_id = '{id}' AND name = 'Ale'");
        lobby.Claim(lobby.Participants.Single(p => p.Name.Value == "Libero").Id, Friend);

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => db.SaveChangesAsync());
        Assert.Equal("lobby_participants_one_per_device", ex.ConstraintName);
    }

    [Fact]
    public async Task Delete_expired_lobbies_removes_only_lobbies_older_than_48_hours()
    {
        var expired = NewLobby(Now.AddHours(-49), now: Now.AddDays(-5));
        var recent = NewLobby(Now.AddHours(-47), now: Now.AddDays(-5));
        await SaveAsync(expired);
        await SaveAsync(recent);

        await using var db = Fixture.NewDbContext();
        var deleted = await db.Database.SqlQuery<int>($"SELECT delete_expired_lobbies() AS \"Value\"").SingleAsync();

        Assert.Equal(1, deleted);
        Assert.Equal([recent.Id], await db.Lobbies.AsNoTracking().Select(l => l.Id).ToListAsync());
        Assert.Single(await db.Set<Participant>().AsNoTracking().ToListAsync());
    }

    private async Task Sql(string sql)
    {
        await using var conn = new NpgsqlConnection(Fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
