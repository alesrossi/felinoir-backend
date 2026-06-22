using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Felinoir.IntegrationTests.Felix;

/// <summary>
/// Drives <c>POST /felix/chat</c> through the real pipeline with an in-memory TestServer.
/// No database or Gemini key is required: validation (400) happens before any DB access,
/// and an unset GEMINI_API_KEY deterministically yields the 503 path.
/// </summary>
public class FelixEndpointTests : IClassFixture<FelixEndpointTests.Factory>
{
    private readonly HttpClient _client;

    public FelixEndpointTests(Factory factory) => _client = factory.CreateClient();

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

    private sealed record ErrorBody(string Error);

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Added last, so these win over the .env-derived environment variables.
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Parseable but never connected to for these tests.
                ["DATABASE_URL"] = "Host=localhost;Port=5432;Database=felinoir;Username=u;Password=p",
                ["GEMINI_API_KEY"] = "",
            }));
        }
    }
}
