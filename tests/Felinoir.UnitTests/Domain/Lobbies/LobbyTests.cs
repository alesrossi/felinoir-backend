using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;

namespace Felinoir.UnitTests.Domain.Lobbies;

/// <summary>The Lobby aggregate's rules, without a database.</summary>
public class LobbyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartsAt = Now.AddHours(1);

    private static readonly DeviceHash Me = DeviceHash.FromToken("me");
    private static readonly DeviceHash Friend = DeviceHash.FromToken("friend");
    private static readonly DeviceHash Stranger = DeviceHash.FromToken("stranger");

    private static readonly ScreeningSnapshot Screening = new()
    {
        ScreeningId = 12345,
        MovieId = 42,
        MovieTitle = "Dune - Parte due",
        CinemaId = "nuovo-sacher",
        CinemaName = "Nuovo Sacher",
    };

    private static ParticipantName Name(string text)
    {
        Assert.True(ParticipantName.TryCreate(text, out var name), text);
        return name;
    }

    private static LobbyNote Note(string text)
    {
        Assert.True(LobbyNote.TryCreate(text, out var note), text);
        return note;
    }

    private static Lobby NewLobby() =>
        Lobby.Create(Screening, StartsAt, Name("Ale"), Me, LobbyNote.Empty, Now).Value;

    private static Participant Entry(Lobby lobby, string name) => lobby.Participants.Single(p => p.Name.Value == name);

    private static void AssertError(Error expected, Result result) => Assert.Same(expected, result.Error);

    // ── Creating ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_lobby_has_its_creator_as_first_participant_at_version_1()
    {
        var lobby = Lobby.Create(Screening, StartsAt, Name("Ale"), Me, Note("Ci vediamo"), Now).Value;

        var creator = Assert.Single(lobby.Participants);
        Assert.Equal("Ale", creator.Name.Value);
        Assert.True(creator.IsHeldBy(Me));
        Assert.Equal(Now, creator.JoinedAt);
        Assert.Equal(1, lobby.Version);
        Assert.Equal("Ci vediamo", lobby.Note.Value);
        Assert.Same(Screening, lobby.Screening);
        Assert.Equal(StartsAt + TimeSpan.FromHours(48), lobby.ExpiresAt);
    }

    [Fact]
    public void A_lobby_cannot_be_created_for_a_screening_that_started()
    {
        AssertError(LobbyErrors.ScreeningPast, Lobby.Create(Screening, Now, Name("Ale"), Me, LobbyNote.Empty, Now));
        AssertError(LobbyErrors.ScreeningPast, Lobby.Create(Screening, Now.AddMinutes(-1), Name("Ale"), Me, LobbyNote.Empty, Now));
    }

    [Fact]
    public void A_lobby_expires_48_hours_after_its_screening_starts()
    {
        var lobby = NewLobby();

        Assert.False(lobby.IsExpiredAt(StartsAt.AddHours(1)));            // usable after the start
        Assert.False(lobby.IsExpiredAt(StartsAt + Lobby.Lifetime));
        Assert.True(lobby.IsExpiredAt(StartsAt + Lobby.Lifetime + TimeSpan.FromSeconds(1)));
    }

    // ── Joining ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_device_joins_once_under_a_free_name()
    {
        var lobby = NewLobby();

        Assert.True(lobby.Join(Name("Giulia"), Friend, Now.AddMinutes(1)).IsSuccess);

        Assert.True(Entry(lobby, "Giulia").IsHeldBy(Friend));
        Assert.Equal(2, lobby.Version);
        AssertError(LobbyErrors.AlreadyJoined, lobby.Join(Name("Altro"), Friend, Now));
        Assert.Equal(2, lobby.Version);
    }

    [Fact]
    public void Names_are_taken_ignoring_case()
    {
        var lobby = NewLobby();

        AssertError(LobbyErrors.NameTaken, lobby.Join(Name("ALE"), Friend, Now));
        Assert.Single(lobby.Participants);
        Assert.Equal(1, lobby.Version);
    }

    // ── Renaming and removing ──────────────────────────────────────────────────

    [Fact]
    public void Anyone_is_renamed_to_a_free_name_and_may_re_case_their_own()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);
        var giulia = Entry(lobby, "Giulia").Id;

        Assert.True(lobby.Rename(giulia, Name("Giuly")).IsSuccess);
        Assert.True(lobby.Rename(giulia, Name("giuly")).IsSuccess);

        Assert.Equal("giuly", Entry(lobby, "giuly").Name.Value);
        Assert.Equal(4, lobby.Version);
    }

    [Fact]
    public void A_rename_to_someone_elses_name_is_refused()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);

        AssertError(LobbyErrors.NameTaken, lobby.Rename(Entry(lobby, "Giulia").Id, Name("ale")));
        Assert.Equal(2, lobby.Version);
    }

    [Fact]
    public void A_rename_to_the_same_name_changes_nothing()
    {
        var lobby = NewLobby();

        Assert.True(lobby.Rename(Entry(lobby, "Ale").Id, Name("Ale")).IsSuccess);
        Assert.Equal(1, lobby.Version);
    }

    [Fact]
    public void Anyone_is_removed_and_unknown_participants_are_not_found()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);

        Assert.True(lobby.Remove(Entry(lobby, "Giulia").Id).IsSuccess);

        Assert.DoesNotContain(lobby.Participants, p => p.Name.Value == "Giulia");
        Assert.Equal(3, lobby.Version);
        var unknown = ParticipantId.New();
        AssertError(LobbyErrors.NotFound, lobby.Remove(unknown));
        AssertError(LobbyErrors.NotFound, lobby.Rename(unknown, Name("X")));
        AssertError(LobbyErrors.NotFound, lobby.Claim(unknown, Me));
        Assert.Equal(3, lobby.Version);
    }

    // ── Claiming ───────────────────────────────────────────────────────────────

    [Fact]
    public void Claiming_moves_the_device_and_releases_its_other_entry_in_one_change()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);

        Assert.True(lobby.Claim(Entry(lobby, "Ale").Id, Friend).IsSuccess);

        Assert.True(Entry(lobby, "Ale").IsHeldBy(Friend));     // the claimer has Ale now
        Assert.Null(Entry(lobby, "Giulia").Device);            // its old entry is free
        Assert.Null(lobby.EntryOf(Me));                        // Ale's previous holder has nothing
        Assert.Equal(3, lobby.Version);
    }

    [Fact]
    public void A_device_with_no_entry_can_claim_a_free_one()
    {
        var lobby = NewLobby();
        lobby.Join(Name("Giulia"), Friend, Now);
        lobby.Claim(Entry(lobby, "Ale").Id, Friend);            // Giulia is now free

        Assert.True(lobby.Claim(Entry(lobby, "Giulia").Id, Stranger).IsSuccess);

        Assert.True(Entry(lobby, "Giulia").IsHeldBy(Stranger));
        Assert.Equal(4, lobby.Version);
    }

    [Fact]
    public void Claiming_ones_own_entry_changes_nothing()
    {
        var lobby = NewLobby();

        Assert.True(lobby.Claim(Entry(lobby, "Ale").Id, Me).IsSuccess);
        Assert.Equal(1, lobby.Version);
    }

    // ── The note ───────────────────────────────────────────────────────────────

    [Fact]
    public void The_note_changes_only_when_it_differs()
    {
        var lobby = NewLobby();

        Assert.True(lobby.ChangeNote(Note("Ingresso")).IsSuccess);
        Assert.True(lobby.ChangeNote(Note("Ingresso")).IsSuccess);

        Assert.Equal("Ingresso", lobby.Note.Value);
        Assert.Equal(2, lobby.Version);
    }
}
