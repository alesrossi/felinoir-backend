using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Felinoir.IntegrationTests;

/// <summary>
/// Verifies the OpenAPI document is generated and exposes every endpoint, and that the
/// Scalar reference UI is served. Runs on an in-memory TestServer — no DB or Gemini key.
/// </summary>
public class OpenApiTests : IClassFixture<OpenApiTests.Factory>
{
    private readonly HttpClient _client;

    public OpenApiTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Openapi_document_lists_every_endpoint()
    {
        var res = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();

        Assert.Contains("Felinoir API", json);
        foreach (var path in new[] { "/health", "/movies", "/movies/slug/{slug}", "/movies/{id}", "/cinemas", "/cinemas/{id}", "/screenings", "/felix/chat" })
            Assert.Contains($"\"{path}\"", json);

        // Schemas for the documented response/request types are emitted too.
        Assert.Contains("ErrorResponse", json);
        Assert.Contains("FelixChatRequest", json);
    }

    [Fact]
    public async Task Scalar_reference_ui_is_served()
    {
        var res = await _client.GetAsync("/scalar/v1");

        Assert.True(res.IsSuccessStatusCode, $"Scalar UI returned {(int)res.StatusCode}");
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "Host=localhost;Port=5432;Database=felinoir;Username=u;Password=p",
                ["GEMINI_API_KEY"] = "",
            }));
        }
    }
}
