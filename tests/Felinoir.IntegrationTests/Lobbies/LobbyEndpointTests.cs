using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Felinoir.Application.Lobbies;
using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Lobbies;

/// <summary>
/// Drives the <c>/lobbies</c> endpoints through the real pipeline and Postgres, including
/// the triggers that bump <c>version</c> and the unique indexes behind the 409s.
/// Responses are read as raw JSON to pin the wire shape the frontend relies on.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LobbyEndpointTests : PostgresTestBase, IAsyncLifetime
{
    private const string Me = "my-device-token";
    private const string Friend = "friend-device-token";

    private const int ScreeningId = 12345;
    private const int PastScreeningId = 999;

    private readonly string _startsAt = RomeTime.ToWallClock(DateTimeOffset.UtcNow.AddDays(1));

    public LobbyEndpointTests(PostgresFixture fixture) : base(fixture) { }

    // Re-implements IAsyncLifetime so xUnit calls this (reset, then seed) instead of the base method.
    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var future = TestData.Screening(ScreeningId, 42, "nuovo-sacher", _startsAt);
        future.IsOV = true;
        future.BookingUrl = "https://sacher/book";

        var movie = TestData.Movie(42, title: "Dune - Parte due", slug: "dune-parte-due");
        movie.TmdbTitleEn = "Dune: Part Two";

        await SeedAsync(
            movie,
            TestData.Cinema("nuovo-sacher", name: "Nuovo Sacher"),
            future,
            TestData.Screening(PastScreeningId, 42, "nuovo-sacher",
                RomeTime.ToWallClock(DateTimeOffset.UtcNow.AddMinutes(-5))));
    }

    private HttpClient Client(string? deviceToken)
    {
        var client = Fixture.Api.CreateClient();
        if (deviceToken is not null) client.DefaultRequestHeaders.Add("X-Device-Token", deviceToken);
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>());

    private static async Task AssertError(HttpResponseMessage res, HttpStatusCode status, string code)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal(code, (await Json(res)).GetProperty("error").GetString());
    }

    private async Task<JsonElement> CreateLobbyAsync(string name = "Giulia")
    {
        var res = await Client(Me).PostAsJsonAsync("/lobbies", new { screeningId = ScreeningId, name, note = "Alle 20:45" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await Json(res);
    }

    private static string Id(JsonElement lobby) => lobby.GetProperty("id").GetString()!;
    private static long Version(JsonElement lobby) => lobby.GetProperty("version").GetInt64();

    private async Task<JsonElement> JoinAsync(JsonElement lobby, string device, string name)
    {
        var res = await Client(device).PostAsJsonAsync($"/lobbies/{Id(lobby)}/participants", new { name });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await Json(res);
    }

    [Fact]
    public async Task Create_returns_201_with_the_full_lobby()
    {
        var res = await Client(Me).PostAsJsonAsync("/lobbies", new { screeningId = ScreeningId, name = " Giulia ", note = "Alle 20:45" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var lobby = await Json(res);
        Assert.Equal($"/lobbies/{Id(lobby)}", res.Headers.Location!.OriginalString);

        var s = lobby.GetProperty("screening");
        Assert.Equal(ScreeningId, s.GetProperty("screeningId").GetInt32());
        Assert.True(s.GetProperty("available").GetBoolean());
        Assert.Equal(42, s.GetProperty("movieId").GetInt32());
        Assert.Equal("dune-parte-due", s.GetProperty("movieSlug").GetString());
        Assert.Equal("Dune - Parte due", s.GetProperty("movieTitle").GetString());
        Assert.Equal("Dune: Part Two", s.GetProperty("movieTitleEn").GetString());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("posterUrl").ValueKind);
        Assert.Equal("nuovo-sacher", s.GetProperty("cinemaId").GetString());
        Assert.Equal("Nuovo Sacher", s.GetProperty("cinemaName").GetString());
        Assert.Equal(_startsAt, s.GetProperty("datetime").GetString());
        Assert.True(s.GetProperty("isOV").GetBoolean());
        Assert.False(s.GetProperty("is3D").GetBoolean());
        Assert.Equal("https://sacher/book", s.GetProperty("bookingUrl").GetString());

        Assert.Equal("Alle 20:45", lobby.GetProperty("note").GetString());

        var p = Assert.Single(lobby.GetProperty("participants").EnumerateArray());
        Assert.Equal("Giulia", p.GetProperty("name").GetString());
        Assert.True(p.GetProperty("isMe").GetBoolean());
        Assert.EndsWith("Z", p.GetProperty("joinedAt").GetString());
        Assert.Equal(p.GetProperty("id").GetString(), lobby.GetProperty("meId").GetString());

        RomeTime.TryParseWallClock(_startsAt, out var startsAtUtc);
        Assert.Equal(RomeTime.ToUtcString(startsAtUtc.AddHours(48)), lobby.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task Requests_without_a_device_token_are_rejected()
    {
        var lobby = await CreateLobbyAsync();
        var anonymous = Client(null);

        await AssertError(await anonymous.GetAsync($"/lobbies/{Id(lobby)}"), HttpStatusCode.BadRequest, "missing_device");
        await AssertError(
            await anonymous.PostAsJsonAsync("/lobbies", new { screeningId = ScreeningId, name = "Giulia" }),
            HttpStatusCode.BadRequest, "missing_device");
    }

    [Fact]
    public async Task Create_rejects_unknown_and_started_screenings_and_bad_input()
    {
        var client = Client(Me);

        await AssertError(await client.PostAsJsonAsync("/lobbies", new { screeningId = 777, name = "Giulia" }),
            HttpStatusCode.UnprocessableEntity, "screening_not_found");
        await AssertError(await client.PostAsJsonAsync("/lobbies", new { screeningId = PastScreeningId, name = "Giulia" }),
            HttpStatusCode.UnprocessableEntity, "screening_past");
        await AssertError(await client.PostAsJsonAsync("/lobbies", new { screeningId = ScreeningId, name = "" }),
            HttpStatusCode.BadRequest, "invalid_name");
        await AssertError(await client.PostAsJsonAsync("/lobbies", new { screeningId = ScreeningId, name = "Giulia", note = new string('a', 501) }),
            HttpStatusCode.BadRequest, "invalid_note");
    }

    [Fact]
    public async Task Get_from_another_device_has_a_null_meId()
    {
        var created = await CreateLobbyAsync();

        var res = await Client(Friend).GetAsync($"/lobbies/{Id(created)}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lobby = await Json(res);
        Assert.Equal(JsonValueKind.Null, lobby.GetProperty("meId").ValueKind);
        Assert.False(lobby.GetProperty("participants")[0].GetProperty("isMe").GetBoolean());
    }

    [Fact]
    public async Task Polling_returns_304_until_something_changes()
    {
        var created = await CreateLobbyAsync();
        var url = $"/lobbies/{Id(created)}?ifVersion={Version(created)}";

        var unchanged = await Client(Me).GetAsync(url);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync());

        await JoinAsync(created, Friend, "Marco");

        var changed = await Client(Me).GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.True(Version(await Json(changed)) > Version(created));
    }

    [Fact]
    public async Task Unknown_malformed_and_expired_lobbies_are_not_found()
    {
        var expired = new Lobby
        {
            MovieTitle = "Old", CinemaId = "nuovo-sacher", CinemaName = "Nuovo Sacher",
            StartsAt = DateTimeOffset.UtcNow.AddHours(-49), CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
        };
        await SeedAsync(expired);
        var client = Client(Me);

        await AssertError(await client.GetAsync($"/lobbies/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found");
        await AssertError(await client.GetAsync("/lobbies/not-a-uuid"), HttpStatusCode.NotFound, "not_found");
        await AssertError(await client.GetAsync($"/lobbies/{expired.Id}"), HttpStatusCode.NotFound, "not_found");
        await AssertError(await client.PostAsJsonAsync($"/lobbies/{expired.Id}/participants", new { name = "Marco" }),
            HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Note_edits_bump_the_version()
    {
        var created = await CreateLobbyAsync();

        var res = await Client(Friend).PatchAsJsonAsync($"/lobbies/{Id(created)}", new { note = "Ci vediamo all'ingresso" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lobby = await Json(res);
        Assert.Equal("Ci vediamo all'ingresso", lobby.GetProperty("note").GetString());
        Assert.Equal(Version(created) + 1, Version(lobby));

        await AssertError(
            await Client(Friend).PatchAsJsonAsync($"/lobbies/{Id(created)}", new { note = new string('a', 501) }),
            HttpStatusCode.BadRequest, "invalid_note");
    }

    [Fact]
    public async Task Join_adds_this_device_and_rejects_duplicates()
    {
        var created = await CreateLobbyAsync();

        var lobby = await JoinAsync(created, Friend, "Marco");
        Assert.Equal(Version(created) + 1, Version(lobby));
        var participants = lobby.GetProperty("participants");
        Assert.Equal(["Giulia", "Marco"], participants.EnumerateArray().Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(participants[1].GetProperty("id").GetString(), lobby.GetProperty("meId").GetString());

        await AssertError(
            await Client("third-device").PostAsJsonAsync($"/lobbies/{Id(created)}/participants", new { name = "marco" }),
            HttpStatusCode.Conflict, "name_taken");
        await AssertError(
            await Client(Friend).PostAsJsonAsync($"/lobbies/{Id(created)}/participants", new { name = "Luca" }),
            HttpStatusCode.Conflict, "already_joined");
    }

    [Fact]
    public async Task Rename_changes_anyones_name_unless_taken()
    {
        var created = await CreateLobbyAsync();
        var joined = await JoinAsync(created, Friend, "Marco");
        var giulia = created.GetProperty("meId").GetString();
        var url = $"/lobbies/{Id(created)}/participants/{giulia}";

        var res = await Client(Friend).PatchAsJsonAsync(url, new { name = "Giuly" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lobby = await Json(res);
        Assert.Equal("Giuly", lobby.GetProperty("participants")[0].GetProperty("name").GetString());
        Assert.Equal(Version(joined) + 1, Version(lobby));

        await AssertError(await Client(Me).PatchAsJsonAsync(url, new { name = "MARCO" }), HttpStatusCode.Conflict, "name_taken");
        await AssertError(
            await Client(Me).PatchAsJsonAsync($"/lobbies/{Id(created)}/participants/{Guid.NewGuid()}", new { name = "X" }),
            HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Delete_removes_a_participant()
    {
        var created = await CreateLobbyAsync();

        var res = await Client(Me).DeleteAsync($"/lobbies/{Id(created)}/participants/{created.GetProperty("meId").GetString()}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lobby = await Json(res);
        Assert.Empty(lobby.GetProperty("participants").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, lobby.GetProperty("meId").ValueKind);
        Assert.Equal(Version(created) + 1, Version(lobby));
    }

    [Fact]
    public async Task Claim_moves_this_device_to_the_entry()
    {
        var created = await CreateLobbyAsync();
        var joined = await JoinAsync(created, Friend, "Marco");
        var marco = joined.GetProperty("meId").GetString();

        var res = await Client(Me).PostAsync($"/lobbies/{Id(created)}/participants/{marco}/claim", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lobby = await Json(res);
        Assert.Equal(marco, lobby.GetProperty("meId").GetString());
        Assert.Equal([false, true], lobby.GetProperty("participants").EnumerateArray().Select(p => p.GetProperty("isMe").GetBoolean()));
        // Clearing the old entry and taking the new one are two changes.
        Assert.Equal(Version(joined) + 2, Version(lobby));

        var friendView = await Json(await Client(Friend).GetAsync($"/lobbies/{Id(created)}"));
        Assert.Equal(JsonValueKind.Null, friendView.GetProperty("meId").ValueKind);
    }

    [Fact]
    public async Task Lobby_outlives_its_screening_in_the_schedule()
    {
        var created = await CreateLobbyAsync();
        await using (var db = Fixture.NewDbContext())
        {
            db.Screenings.Remove(db.Screenings.Single(s => s.Id == ScreeningId));
            await db.SaveChangesAsync();
        }

        var lobby = await Json(await Client(Me).GetAsync($"/lobbies/{Id(created)}"));

        var s = lobby.GetProperty("screening");
        Assert.False(s.GetProperty("available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, s.GetProperty("screeningId").ValueKind);
        Assert.Equal("Dune - Parte due", s.GetProperty("movieTitle").GetString());
    }
}
