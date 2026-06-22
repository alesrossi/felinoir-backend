using Felinoir.Application.Screenings;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Services;

[Collection(PostgresCollection.Name)]
public class ScreeningServiceTests : PostgresTestBase
{
    public ScreeningServiceTests(PostgresFixture fixture) : base(fixture) { }

    private ScreeningService NewService() => new(Fixture.NewDbContext());

    /// <summary>Two cinemas in different cities and two movies, with a handful of screenings.</summary>
    private async Task SeedGraphAsync()
    {
        await SeedAsync(
            TestData.Cinema("roma-1", city: "Roma"),
            TestData.Cinema("milano-1", city: "Milano"),
            TestData.Movie(10, "Movie A"),
            TestData.Movie(20, "Movie B"));

        await SeedAsync(
            TestData.Screening(1, movieId: 10, cinemaId: "roma-1", datetime: "2026-06-22T20:00:00Z"),
            TestData.Screening(2, movieId: 20, cinemaId: "roma-1", datetime: "2026-06-23T18:00:00Z"),
            TestData.Screening(3, movieId: 10, cinemaId: "milano-1", datetime: "2026-06-24T21:00:00Z"),
            TestData.Screening(4, movieId: 20, cinemaId: "milano-1", datetime: "2026-06-25T19:00:00Z"));
    }

    [Fact]
    public async Task GetAsync_with_no_filters_returns_all_ordered_by_datetime()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery());

        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_filters_by_cinema()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { CinemaId = "milano-1" });

        Assert.Equal(new[] { 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_filters_by_movie()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { MovieId = 10 });

        Assert.Equal(new[] { 1, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_filters_by_city_via_cinema_relation()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { City = "Milano" });

        Assert.Equal(new[] { 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_from_bound_is_inclusive()
    {
        await SeedGraphAsync();

        // From exactly matches screening 2's datetime — it must be included.
        var result = await NewService().GetAsync(new ScreeningQuery { From = "2026-06-23T18:00:00Z" });

        Assert.Equal(new[] { 2, 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_to_bound_is_inclusive()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { To = "2026-06-24T21:00:00Z" });

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_from_and_to_select_inclusive_range()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery
        {
            From = "2026-06-23T00:00:00Z",
            To = "2026-06-24T23:59:59Z",
        });

        Assert.Equal(new[] { 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_combines_filters()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery
        {
            City = "Roma",
            MovieId = 20,
        });

        Assert.Equal(new[] { 2 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_without_relations_leaves_navigations_null()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { WithRelations = false });

        Assert.All(result, s =>
        {
            Assert.Null(s.Movie);
            Assert.Null(s.Cinema);
        });
    }

    [Fact]
    public async Task GetAsync_with_relations_populates_movie_and_cinema()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { WithRelations = true });

        Assert.All(result, s =>
        {
            Assert.NotNull(s.Movie);
            Assert.NotNull(s.Cinema);
        });
        var first = result.First(s => s.Id == 1);
        Assert.Equal("Movie A", first.Movie!.Title);
        Assert.Equal("Roma", first.Cinema!.City);
    }

    [Fact]
    public async Task GetAsync_applies_limit_and_offset_over_datetime_order()
    {
        await SeedGraphAsync();

        // Ordered [1,2,3,4]; skip 1, take 2 => [2,3].
        var result = await NewService().GetAsync(new ScreeningQuery { Offset = 1, Limit = 2 });

        Assert.Equal(new[] { 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_returns_empty_when_no_screenings_match()
    {
        await SeedGraphAsync();

        var result = await NewService().GetAsync(new ScreeningQuery { CinemaId = "nonexistent" });

        Assert.Empty(result);
    }
}
