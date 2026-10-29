using System.Text.Json;
using Felinoir.Application.Features.Felix;
using Felinoir.Infrastructure.Persistence;
using Felinoir.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static VerifyXunit.Verifier;

namespace Felinoir.IntegrationTests.Contract;

/// <summary>
/// Characterisation snapshots of the whole HTTP contract (rework plan, step 0). Each test
/// drives one resource through the real pipeline and Postgres against <see cref="ContractData"/>
/// and verifies every exchange (status, Content-Type/Location, body) against the committed
/// <c>Snapshots/*.verified.txt</c>.
///
/// The rework must keep these snapshots unchanged. A deliberate, additive contract change
/// updates the affected snapshot in the same commit, and the diff is the review.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContractTests(PostgresFixture fixture) : PostgresTestBase(fixture), IAsyncLifetime
{
    private const string Me = "contract-device-me";
    private const string Friend = "contract-device-friend";
    private const string Stranger = "contract-device-stranger";

    private readonly MovableTimeProvider _clock = new(ContractData.Now);
    private readonly RecordingGeminiClient _gemini = new();
    private PostgresApiFactory? _api;

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedAsync(ContractData.All());
    }

    public new async Task DisposeAsync()
    {
        if (_api is not null) await _api.DisposeAsync();
    }

    /// <summary>
    /// A fresh API per test, so Felix's process-wide state never leaks between tests. It runs as
    /// Production, so framework errors (e.g. malformed JSON) look as they do in production
    /// rather than as the developer exception page.
    /// </summary>
    private ContractRecorder Recorder()
    {
        _api ??= new PostgresApiFactory(Fixture.ConnectionString, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_clock);
            services.RemoveAll<IGeminiClient>();
            services.AddSingleton<IGeminiClient>(_gemini);
        }, environment: "Production");
        return new ContractRecorder(_api.CreateClient());
    }

    private static Task Snapshot(ContractRecorder recorder) =>
        Verify(recorder.Render()).UseDirectory("Snapshots");

    [Fact]
    public async Task Health()
    {
        var r = Recorder();
        await r.GetAsync("/health");
        await r.GetAsync("/no/such/route", label: "unknown route");
        await r.SendAsync(HttpMethod.Delete, "/health", label: "method not allowed");
        await Snapshot(r);
    }

    [Fact]
    public async Task Movies()
    {
        var r = Recorder();
        await r.GetAsync("/movies");
        await r.GetAsync("/movies?limit=2");
        await r.GetAsync("/movies?limit=2&offset=2");
        await r.GetAsync("/movies?limit=0");
        await r.GetAsync("/movies?limit=-5&offset=-3", label: "both clamped");
        await r.GetAsync("/movies?limit=99999", label: "clamped to max");
        await r.GetAsync("/movies?limit=abc", label: "unparseable");
        await r.GetAsync($"/movies/{ContractData.Dune}");
        await r.GetAsync($"/movies/{ContractData.Shorts}");
        await r.GetAsync("/movies/999");
        await r.GetAsync("/movies/abc");
        await r.GetAsync("/movies/slug/dune-parte-due");
        await r.GetAsync("/movies/slug/la-chimera");
        await r.GetAsync("/movies/slug/does-not-exist");
        await r.GetAsync("/movies?city=rome", label: "every movie with a screening in Rome");
        await r.GetAsync("/movies?city=Milano", label: "unknown city");
        await Snapshot(r);
    }

    [Fact]
    public async Task Cinemas()
    {
        var r = Recorder();
        await r.GetAsync("/cinemas");
        await r.GetAsync($"/cinemas/{ContractData.Adriano}");
        await r.GetAsync($"/cinemas/{ContractData.Augustus}");
        await r.GetAsync("/cinemas/does-not-exist");
        await r.GetAsync("/cinemas?city=Roma");
        await r.GetAsync("/cinemas?city=milano", label: "unknown city");
        await Snapshot(r);
    }

    [Fact]
    public async Task Screenings()
    {
        var r = Recorder();
        await r.GetAsync("/screenings");
        await r.GetAsync($"/screenings?cinemaId={ContractData.Adriano}");
        await r.GetAsync($"/screenings?movieId={ContractData.Dune}");
        await r.GetAsync("/screenings?city=Roma");
        await r.GetAsync("/screenings?city=rome", label: "city id");
        await r.GetAsync("/screenings?city=ROMA", label: "name, any case");
        await r.GetAsync("/screenings?city=Velletri", label: "a town, not a city: Augustus is a Rome cinema");
        await r.GetAsync("/screenings?city=Milano");
        await r.GetAsync("/screenings?from=2099-06-02T19:30:00Z", label: "UTC bound, 21:30 in Rome");
        await r.GetAsync("/screenings?from=2099-06-02T00:00:00");
        await r.GetAsync("/screenings?to=2099-06-01T23:59:59");
        await r.GetAsync("/screenings?from=2099-06-01T00:00:00&to=2099-06-02T23:59:59&cinemaId=cinema-adriano");
        await r.GetAsync($"/screenings?withRelations=true&cinemaId={ContractData.SanCosimato}");
        await r.GetAsync("/screenings?withRelations=false&movieId=2");
        await r.GetAsync("/screenings?limit=2&offset=1");
        await r.GetAsync("/screenings?limit=-5", label: "clamped to 0");
        await r.GetAsync("/screenings?offset=-3", label: "clamped to 0");
        await Snapshot(r);
    }

    [Fact]
    public async Task Felix_inline_context()
    {
        var r = Recorder();
        var hello = new[] { new { role = "user", content = "Cosa c'è stasera?" } };

        await r.SendAsync(HttpMethod.Post, "/felix/chat", "{}", label: "no messages");
        await r.SendAsync(HttpMethod.Post, "/felix/chat", new { messages = Array.Empty<object>() });
        await r.SendAsync(HttpMethod.Post, "/felix/chat",
            new { messages = new[] { new { role = "system", content = "hi" } } });
        await r.SendAsync(HttpMethod.Post, "/felix/chat",
            new { messages = new[] { new { role = "user", content = "" } } });
        await r.SendAsync(HttpMethod.Post, "/felix/chat", "not json", label: "malformed body");
        r.Note("Gemini calls after the invalid requests", _gemini.TakeLog());

        await r.SendAsync(HttpMethod.Post, "/felix/chat", new { messages = hello }, label: "cold start");
        r.Note("Gemini calls", _gemini.TakeLog());

        await r.SendAsync(HttpMethod.Post, "/felix/chat", new
        {
            messages = new object[]
            {
                new { role = "user", content = "Cosa c'è stasera?" },
                new { role = "assistant", content = "Dune all'Adriano." },
                new { role = "user", content = "E all'aperto?" },
            },
            filters = new
            {
                cinemas = new[] { "Piazza San Cosimato" },
                dates = new[] { "2099-06-02" },
                times = new[] { "sera" },
                genres = new[] { "Fantascienza" },
                ov = true,
                q = "deserto",
            },
        }, label: "warm, with filters");
        r.Note("Gemini calls", _gemini.TakeLog());

        await r.SendAsync(HttpMethod.Post, "/felix/chat",
            new { messages = new[] { new { role = "user", content = "Ciao" } }, filters = new { } },
            label: "empty filters");
        r.Note("Gemini calls", _gemini.TakeLog());

        await r.SendAsync(HttpMethod.Post, "/felix/chat",
            new { messages = new[] { new { role = "user", content = RecordingGeminiClient.FailTrigger } } },
            label: "Gemini fails");
        r.Note("Gemini calls", _gemini.TakeLog());

        await r.SendAsync(HttpMethod.Post, "/felix/chat?city=rome", new { messages = hello }, label: "city id");
        r.Note("Gemini calls", _gemini.TakeLog());
        await r.SendAsync(HttpMethod.Post, "/felix/chat?city=Torino", new { messages = hello }, label: "unknown city");
        r.Note("Gemini calls", _gemini.TakeLog());

        _gemini.IsConfigured = false;
        await r.SendAsync(HttpMethod.Post, "/felix/chat", new { messages = hello }, label: "no API key");
        await r.SendAsync(HttpMethod.Post, "/felix/chat", "{}", label: "no API key, invalid body");

        await Snapshot(r);
    }

    [Fact]
    public async Task Felix_context_cache()
    {
        _gemini.CacheToCreate = new GeminiCacheRef(
            "cachedContents/contract", new DateTimeOffset(2099, 12, 31, 0, 0, 0, TimeSpan.Zero));
        var r = Recorder();
        var hello = new[] { new { role = "user", content = "Cosa c'è stasera?" } };

        await r.SendAsync(HttpMethod.Post, "/felix/chat", new { messages = hello }, label: "cold start");
        r.Note("Gemini calls", _gemini.TakeLog());
        r.Note("felix_cache", await FelixCacheRowsAsync());

        await r.SendAsync(HttpMethod.Post, "/felix/chat", new { messages = hello }, label: "warm");
        r.Note("Gemini calls", _gemini.TakeLog());

        await Snapshot(r);
    }

    [Fact]
    public async Task Lobby_lifecycle()
    {
        var r = Recorder();

        var created = await r.SendAsync(HttpMethod.Post, "/lobbies",
            new { screeningId = ContractData.EveningScreening, name = " Ale ", note = "Ci vediamo all'ingresso" },
            Me);
        var id = created!.Value.GetProperty("id").GetString()!;
        var url = $"/lobbies/{id}";
        var ale = Participant(created.Value, "Ale");

        var current = await r.GetAsync(url, Me);
        var version = current!.Value.GetProperty("version").GetInt64();
        await r.GetAsync($"{url}?ifVersion={version}", Me, label: "unchanged");
        await r.GetAsync($"{url}?ifVersion={version - 1}", Me, label: "stale version");

        _clock.Now += TimeSpan.FromMinutes(1);
        var joined = await r.SendAsync(HttpMethod.Post, $"{url}/participants", new { name = "Giulia" }, Friend);
        var giulia = Participant(joined!.Value, "Giulia");

        await r.GetAsync(url, Stranger, label: "a device with no entry");
        await r.SendAsync(HttpMethod.Patch, url, new { note = "Nuova nota" }, Friend);
        await r.SendAsync(HttpMethod.Patch, url, new { note = "" }, Friend, label: "empty note is valid");
        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/{giulia}", new { name = "  Giuly  " }, Me,
            label: "rename someone else");
        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/{giulia}", new { name = "giuly" }, Friend,
            label: "rename to own name, different case");
        await r.SendAsync(HttpMethod.Post, $"{url}/participants/{ale}/claim", null, Friend,
            label: "friend claims Ale, releasing Giuly");
        await r.SendAsync(HttpMethod.Post, $"{url}/participants/{ale}/claim", null, Friend,
            label: "claiming own entry again");
        await r.SendAsync(HttpMethod.Post, $"{url}/participants/{giulia}/claim", null, Me,
            label: "me claims the released entry");
        await r.SendAsync(HttpMethod.Delete, $"{url}/participants/{giulia}", null, Me, label: "leave");
        await r.GetAsync(url, Me, label: "me has no entry any more");

        await RemoveScreeningAsync(ContractData.EveningScreening);
        await r.GetAsync(url, Friend, label: "screening left the schedule");

        _clock.Now = new DateTimeOffset(2099, 6, 3, 18, 59, 0, TimeSpan.Zero);
        await r.GetAsync(url, Friend, label: "47h59m after the start");
        _clock.Now = new DateTimeOffset(2099, 6, 3, 19, 0, 1, TimeSpan.Zero);
        await r.GetAsync(url, Friend, label: "just past 48h: expired");
        await r.SendAsync(HttpMethod.Patch, url, new { note = "troppo tardi" }, Friend, label: "edit when expired");

        await Snapshot(r);
    }

    [Fact]
    public async Task Lobby_errors()
    {
        var r = Recorder();
        var valid = new { screeningId = ContractData.EveningScreening, name = "Ale" };

        await r.SendAsync(HttpMethod.Post, "/lobbies", valid, deviceToken: null, label: "no device token");
        await r.SendAsync(HttpMethod.Post, "/lobbies", valid, deviceToken: "", label: "empty device token");
        await r.SendAsync(HttpMethod.Post, "/lobbies", "{}", Me, label: "empty body");
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = "" }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = "   " }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = new string('a', 41) }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = new string('a', 40) }, Me,
            label: "40 chars is valid");
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = "Al\te" }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 1, name = "Ale", note = new string('n', 501) }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { screeningId = 999, name = "Ale" }, Me);
        await r.SendAsync(HttpMethod.Post, "/lobbies", new { name = "Ale" }, Me, label: "no screening id");
        await r.SendAsync(HttpMethod.Post, "/lobbies",
            new { screeningId = ContractData.MorningScreening, name = "Ale" }, Me, label: "already started");
        await r.SendAsync(HttpMethod.Post, "/lobbies",
            new { screeningId = ContractData.OldScreening, name = "Ale" }, Me, label: "long past");

        await r.GetAsync("/lobbies/not-a-guid", Me);
        await r.GetAsync("/lobbies/00000000-0000-0000-0000-000000000000", Me);
        await r.GetAsync("/lobbies/00000000-0000-0000-0000-000000000000", deviceToken: null,
            label: "unknown lobby, no device");

        var created = await r.SendAsync(HttpMethod.Post, "/lobbies", valid, Me);
        var url = $"/lobbies/{created!.Value.GetProperty("id").GetString()}";
        var ale = Participant(created.Value, "Ale");
        const string unknown = "11111111-1111-1111-1111-111111111111";

        await r.SendAsync(HttpMethod.Post, $"{url}/participants", new { name = "Altro" }, Me, label: "same device again");
        await r.SendAsync(HttpMethod.Post, $"{url}/participants", new { name = "ALE" }, Friend, label: "name taken, any case");
        await r.SendAsync(HttpMethod.Post, $"{url}/participants", new { name = "" }, Friend);
        await r.SendAsync(HttpMethod.Post, $"{url}/participants", "{}", Friend, label: "no name");
        // Participants are ordered by join time; equal times fall back to the random id.
        _clock.Now += TimeSpan.FromMinutes(1);
        var joined = await r.SendAsync(HttpMethod.Post, $"{url}/participants", new { name = "Giulia" }, Friend);
        var giulia = Participant(joined!.Value, "Giulia");

        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/{giulia}", new { name = "ale" }, Friend,
            label: "rename onto a taken name");
        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/{giulia}", new { name = "" }, Friend);
        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/{unknown}", new { name = "X" }, Friend);
        await r.SendAsync(HttpMethod.Patch, $"{url}/participants/not-a-guid", new { name = "X" }, Friend);
        await r.SendAsync(HttpMethod.Delete, $"{url}/participants/{unknown}", null, Friend);
        await r.SendAsync(HttpMethod.Post, $"{url}/participants/{unknown}/claim", null, Friend);
        await r.SendAsync(HttpMethod.Patch, url, new { note = new string('n', 501) }, Friend);
        await r.SendAsync(HttpMethod.Patch, url, "{}", Friend, label: "no note");
        await r.SendAsync(HttpMethod.Patch, url, new { note = new string('n', 500) }, Friend, label: "500 chars is valid");
        await r.SendAsync(HttpMethod.Delete, $"{url}/participants/{ale}", null, deviceToken: null, label: "no device");

        await Snapshot(r);
    }

    private static string Participant(JsonElement lobby, string name) =>
        lobby.GetProperty("participants").EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == name)
            .GetProperty("id").GetString()!;

    private async Task RemoveScreeningAsync(int id)
    {
        await using var db = Fixture.NewDbContext();
        await db.Screenings.Where(s => s.Id == id).ExecuteDeleteAsync();
    }

    private async Task<string> FelixCacheRowsAsync()
    {
        await using var db = Fixture.NewDbContext();
        var rows = await db.FelixCache.AsNoTracking().OrderBy(f => f.CityId).ToListAsync();
        return string.Join('\n', rows.Select(f => $"city_id={f.CityId} cache_name={f.CacheName} expires_at={f.ExpiresAt}"));
    }
}
