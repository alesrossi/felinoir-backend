using Felinoir.Application.Common;
using Felinoir.Application.Features;
using Felinoir.Application.Features.Movies;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Features;

/// <summary>The movie query handlers against real Postgres.</summary>
[Collection(PostgresCollection.Name)]
public class MovieQueryTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static PageRequest Page(int limit = 50, int offset = 0) =>
        PageRequest.Create(limit, offset, GetMoviesHandler.DefaultLimit, GetMoviesHandler.MaxLimit);

    private async Task<IReadOnlyList<MovieResponse>> GetMovies(GetMoviesQuery query)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetMoviesHandler(db).Handle(query, default);
    }

    private async Task<Felinoir.Application.Common.Results.Result<MovieResponse>> GetById(int id)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetMovieByIdHandler(db).Handle(new GetMovieByIdQuery(id), default);
    }

    private async Task<Felinoir.Application.Common.Results.Result<MovieResponse>> GetBySlug(string slug)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetMovieBySlugHandler(db).Handle(new GetMovieBySlugQuery(slug), default);
    }

    [Fact]
    public async Task Movies_are_ordered_by_created_at_desc_then_id_desc()
    {
        await SeedAsync(
            TestData.Movie(1, "Oldest", createdAt: "2026-01-01T00:00:00Z"),
            TestData.Movie(2, "Newest", createdAt: "2026-03-01T00:00:00Z"),
            TestData.Movie(3, "Middle", createdAt: "2026-02-01T00:00:00Z"));

        var result = await GetMovies(new GetMoviesQuery(Page()));

        Assert.Equal([2, 3, 1], result.Select(m => m.Id));
    }

    [Fact]
    public async Task Created_at_ties_are_broken_by_id_desc()
    {
        const string sameTime = "2026-01-01T00:00:00Z";
        await SeedAsync(
            TestData.Movie(1, createdAt: sameTime),
            TestData.Movie(2, createdAt: sameTime),
            TestData.Movie(3, createdAt: sameTime));

        var result = await GetMovies(new GetMoviesQuery(Page()));

        Assert.Equal([3, 2, 1], result.Select(m => m.Id));
    }

    [Fact]
    public async Task The_page_applies_limit_and_offset()
    {
        await SeedAsync(
            TestData.Movie(1, createdAt: "2026-01-01T00:00:00Z"),
            TestData.Movie(2, createdAt: "2026-02-01T00:00:00Z"),
            TestData.Movie(3, createdAt: "2026-03-01T00:00:00Z"),
            TestData.Movie(4, createdAt: "2026-04-01T00:00:00Z"));

        // Ordered newest-first => [4,3,2,1]; skip 1, take 2 => [3,2].
        var result = await GetMovies(new GetMoviesQuery(Page(limit: 2, offset: 1)));

        Assert.Equal([3, 2], result.Select(m => m.Id));
    }

    [Fact]
    public async Task No_movies_is_an_empty_list() =>
        Assert.Empty(await GetMovies(new GetMoviesQuery(Page())));

    [Fact]
    public async Task A_city_keeps_only_movies_with_a_screening_there()
    {
        await SeedAsync(TestData.City("milan", "Milano"));
        await SeedAsync(
            TestData.Cinema("roma-1"),
            TestData.Cinema("milano-1", cityId: "milan"),
            TestData.Movie(1, "In Rome"),
            TestData.Movie(2, "In Milan"),
            TestData.Movie(3, "Nowhere"));
        await SeedAsync(
            TestData.Screening(1, 1, "roma-1", "2099-01-01T21:00:00"),
            TestData.Screening(2, 2, "milano-1", "2099-01-01T21:00:00"),
            TestData.Screening(3, 1, "milano-1", "2099-01-02T21:00:00"));

        Assert.Equal([1], (await GetMovies(new GetMoviesQuery(Page(), "rome"))).Select(m => m.Id));
        Assert.Equal([2, 1], (await GetMovies(new GetMoviesQuery(Page(), "MILANO"))).Select(m => m.Id));
        Assert.Empty(await GetMovies(new GetMoviesQuery(Page(), "Torino")));
    }

    [Fact]
    public async Task A_movie_is_found_by_id()
    {
        await SeedAsync(TestData.Movie(7, "Target"));

        var result = await GetById(7);

        Assert.Equal("Target", result.Value.Title);
    }

    [Fact]
    public async Task An_unknown_id_is_not_found()
    {
        await SeedAsync(TestData.Movie(7));

        Assert.Same(CatalogueErrors.NotFound, (await GetById(999)).Error);
    }

    [Fact]
    public async Task A_movie_is_found_by_slug()
    {
        await SeedAsync(TestData.Movie(1, slug: "the-matrix"), TestData.Movie(2, slug: "inception"));

        Assert.Equal(2, (await GetBySlug("inception")).Value.Id);
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        await SeedAsync(TestData.Movie(1, slug: "the-matrix"));

        Assert.Same(CatalogueErrors.NotFound, (await GetBySlug("does-not-exist")).Error);
    }

    [Fact]
    public async Task Slugs_are_case_sensitive()
    {
        await SeedAsync(TestData.Movie(1, slug: "the-matrix"));

        Assert.False((await GetBySlug("The-Matrix")).IsSuccess);
    }
}
