using System.Net;
using System.Net.Http.Json;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Felix;

/// <summary>
/// Drives <c>POST /felix/chat</c> through the real pipeline with an in-memory TestServer.
/// No database or Gemini key is required: validation (400) happens before any service call,
/// and Felix is stubbed as unconfigured so the 503 path is deterministic.
/// </summary>
public class FelixEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public FelixEndpointTests(TestApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Returns_400_when_messages_missing()
    {
        var res = await _client.PostAsJsonAsync("/felix/chat", new { });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Richiesta non valida.", (await res.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    [Fact]
    public async Task Returns_400_when_messages_empty()
    {
        var res = await _client.PostAsJsonAsync("/felix/chat", new { messages = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Returns_400_when_role_invalid()
    {
        var res = await _client.PostAsJsonAsync("/felix/chat",
            new { messages = new[] { new { role = "system", content = "hi" } } });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Returns_503_when_gemini_not_configured()
    {
        var res = await _client.PostAsJsonAsync("/felix/chat",
            new { messages = new[] { new { role = "user", content = "Cosa c'è stasera?" } } });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        Assert.Equal("Felix non è configurato al momento.", (await res.Content.ReadFromJsonAsync<ErrorBody>())!.Error);
    }

    [Fact]
    public async Task Binds_a_fully_populated_filters_object()
    {
        // Mirrors the frontend's ActiveFilters shape (names/labels, optional fields).
        // Reaches the 503 path, proving the body — including filters — deserialised cleanly.
        var res = await _client.PostAsJsonAsync("/felix/chat", new
        {
            messages = new[] { new { role = "user", content = "Cosa c'è stasera?" } },
            filters = new
            {
                cinemas = new[] { "Cinema Farnese" },
                dates = new[] { "2026-06-22" },
                times = new[] { "sera" },
                genres = new[] { "Horror" },
                ov = true,
                q = "vampiri",
            },
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    private sealed record ErrorBody(string Error);
}
