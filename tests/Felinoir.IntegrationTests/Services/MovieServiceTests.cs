using Felinoir.Application.Movies;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Services;

[Collection(PostgresCollection.Name)]
public class MovieServiceTests : PostgresTestBase
{
    public MovieServiceTests(PostgresFixture fixture) : base(fixture) { }

    private MovieService NewService() => new(Fixture.NewDbContext());

    [Fact]
    public async Task GetAllAsync_orders_by_created_at_desc_then_id_desc()
    {
        await SeedAsync(
            TestData.Movie(1, "Oldest", createdAt: "2026-01-01T00:00:00Z"),
            TestData.Movie(2, "Newest", createdAt: "2026-03-01T00:00:00Z"),
            TestData.Movie(3, "Middle", createdAt: "2026-02-01T00:00:00Z"));

        var result = await NewService().GetAllAsync(limit: 50, offset: 0);

        Assert.Equal(new[] { 2, 3, 1 }, result.Select(m => m.Id));
    }

    [Fact]
    public async Task GetAllAsync_breaks_created_at_ties_by_id_desc()
    {
        const string sameTime = "2026-01-01T00:00:00Z";
        await SeedAsync(
            TestData.Movie(1, createdAt: sameTime),
            TestData.Movie(2, createdAt: sameTime),
            TestData.Movie(3, createdAt: sameTime));

        var result = await NewService().GetAllAsync(limit: 50, offset: 0);

        Assert.Equal(new[] { 3, 2, 1 }, result.Select(m => m.Id));
    }

    [Fact]
    public async Task GetAllAsync_applies_limit_and_offset()
    {
        await SeedAsync(
            TestData.Movie(1, createdAt: "2026-01-01T00:00:00Z"),
            TestData.Movie(2, createdAt: "2026-02-01T00:00:00Z"),
            TestData.Movie(3, createdAt: "2026-03-01T00:00:00Z"),
            TestData.Movie(4, createdAt: "2026-04-01T00:00:00Z"));

        // Ordered newest-first => [4,3,2,1]; skip 1, take 2 => [3,2].
        var result = await NewService().GetAllAsync(limit: 2, offset: 1);

        Assert.Equal(new[] { 3, 2 }, result.Select(m => m.Id));
    }

    [Fact]
    public async Task GetAllAsync_returns_empty_when_no_movies()
    {
        var result = await NewService().GetAllAsync(limit: 50, offset: 0);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_returns_matching_movie()
    {
        await SeedAsync(TestData.Movie(7, "Target"));

        var result = await NewService().GetByIdAsync(7);

        Assert.NotNull(result);
        Assert.Equal("Target", result!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        await SeedAsync(TestData.Movie(7));

        var result = await NewService().GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_returns_matching_movie()
    {
        await SeedAsync(
            TestData.Movie(1, slug: "the-matrix"),
            TestData.Movie(2, slug: "inception"));

        var result = await NewService().GetBySlugAsync("inception");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Id);
    }

    [Fact]
    public async Task GetBySlugAsync_returns_null_when_slug_unknown()
    {
        await SeedAsync(TestData.Movie(1, slug: "the-matrix"));

        var result = await NewService().GetBySlugAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_is_case_sensitive()
    {
        await SeedAsync(TestData.Movie(1, slug: "the-matrix"));

        var result = await NewService().GetBySlugAsync("The-Matrix");

        Assert.Null(result);
    }
}
