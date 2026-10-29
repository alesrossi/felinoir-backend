using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Movies;

/// <summary>
/// Pins the wire format of the film-page fields on the movie detail routes: <c>awards</c>
/// as plain text and <c>tmdbCredits</c> as a raw JSON string, like the other tmdb* blobs.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MovieEndpointTests : PostgresTestBase, IAsyncLifetime
{
    private const string Credits = """
        {"directors":[{"id":137427,"name":"Denis Villeneuve","profile_path":"/denis.jpg"}],
         "writers":[{"id":3,"name":"Frank Herbert","profile_path":null,"jobs":["Novel"]}],
         "cast":[{"id":1190668,"name":"Timothée Chalamet","profile_path":"/tc.jpg","character":"Paul Atreides"}],
         "composers":[{"id":947,"name":"Hans Zimmer","profile_path":null}],
         "cinematographers":[]}
        """;

    public MovieEndpointTests(PostgresFixture fixture) : base(fixture) { }

    // Re-implements IAsyncLifetime so xUnit calls this (reset, then seed) instead of the base method.
    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var movie = new Movie
        {
            Id = 42,
            Title = "Dune - Parte due",
            Slug = "dune-parte-due",
            Awards = "Won 2 Oscars. 126 wins & 375 nominations total",
            TmdbCredits = Credits,
            TmdbBudget = 190_000_000,
            TmdbRevenue = 714_444_358,
            CreatedAt = TestData.Created,
            UpdatedAt = TestData.Created,
        };
        await SeedAsync(movie, TestData.Movie(43, title: "Senza dati", slug: "senza-dati"));
    }

    private async Task<JsonElement> GetMovie(string url)
    {
        var res = await Fixture.Api.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Theory]
    [InlineData("/movies/42")]
    [InlineData("/movies/slug/dune-parte-due")]
    public async Task Detail_routes_return_awards_and_credits(string url)
    {
        var movie = await GetMovie(url);

        Assert.Equal("Won 2 Oscars. 126 wins & 375 nominations total", movie.GetProperty("awards").GetString());
        Assert.Equal(190_000_000, movie.GetProperty("tmdbBudget").GetInt64());
        Assert.Equal(714_444_358, movie.GetProperty("tmdbRevenue").GetInt64());

        // A string holding the JSON, not a nested object.
        var credits = movie.GetProperty("tmdbCredits");
        Assert.Equal(JsonValueKind.String, credits.ValueKind);
        using var parsed = JsonDocument.Parse(credits.GetString()!);
        var director = parsed.RootElement.GetProperty("directors")[0];
        Assert.Equal("Denis Villeneuve", director.GetProperty("name").GetString());
        Assert.Equal("/denis.jpg", director.GetProperty("profile_path").GetString());
        Assert.Equal("Paul Atreides", parsed.RootElement.GetProperty("cast")[0].GetProperty("character").GetString());
        Assert.Equal("Novel", parsed.RootElement.GetProperty("writers")[0].GetProperty("jobs")[0].GetString());
    }

    [Fact]
    public async Task Missing_fields_are_omitted()
    {
        var movie = await GetMovie("/movies/43");

        Assert.False(movie.TryGetProperty("awards", out _));
        Assert.False(movie.TryGetProperty("tmdbCredits", out _));
    }
}
