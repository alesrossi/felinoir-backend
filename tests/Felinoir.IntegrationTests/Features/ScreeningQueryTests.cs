using Felinoir.Application.Common;
using Felinoir.Application.Features.Screenings;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Features;

[Collection(PostgresCollection.Name)]
/// <summary>The screening query handler against real Postgres.</summary>
public class ScreeningQueryTests : PostgresTestBase
{
    public ScreeningQueryTests(PostgresFixture fixture) : base(fixture) { }

    private static readonly PageRequest DefaultPage =
        PageRequest.Create(null, null, GetScreeningsHandler.DefaultLimit, GetScreeningsHandler.MaxLimit);

    private async Task<IReadOnlyList<ScreeningResponse>> Get(GetScreeningsQuery query)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetScreeningsHandler(db).Handle(query, default);
    }

    /// <summary>
    /// Two cinemas in different cities and two movies, with a handful of screenings at Italian
    /// wall-clock times, the form the scrapers store.
    /// </summary>
    private async Task SeedGraphAsync()
    {
        await SeedAsync(TestData.City("milan", "Milano"));
        await SeedAsync(
            TestData.Cinema("roma-1"),
            TestData.Cinema("milano-1", cityId: "milan"),
            TestData.Movie(10, "Movie A"),
            TestData.Movie(20, "Movie B"));

        await SeedAsync(
            TestData.Screening(1, movieId: 10, cinemaId: "roma-1", datetime: "2026-06-22T20:00:00"),
            TestData.Screening(2, movieId: 20, cinemaId: "roma-1", datetime: "2026-06-23T18:00:00"),
            TestData.Screening(3, movieId: 10, cinemaId: "milano-1", datetime: "2026-06-24T21:00:00"),
            TestData.Screening(4, movieId: 20, cinemaId: "milano-1", datetime: "2026-06-25T19:00:00"));
    }

    [Fact]
    public async Task GetAsync_with_no_filters_returns_all_ordered_by_datetime()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage));

        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_filters_by_cinema()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage) { CinemaId = "milano-1" });

        Assert.Equal(new[] { 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_filters_by_movie()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage) { MovieId = 10 });

        Assert.Equal(new[] { 1, 3 }, result.Select(s => s.Id));
    }

    [Theory]
    [InlineData("Milano")]
    [InlineData("milan")]
    [InlineData("MILANO")]
    [InlineData(" milano ")]
    public async Task GetAsync_filters_by_city_id_or_name_ignoring_case(string city)
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage) { City = city });

        Assert.Equal(new[] { 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_for_an_unknown_city_is_empty()
    {
        await SeedGraphAsync();

        Assert.Empty(await Get(new GetScreeningsQuery(DefaultPage) { City = "Torino" }));
    }

    [Fact]
    public async Task GetAsync_converts_a_utc_bound_to_italian_time()
    {
        await SeedGraphAsync();

        // 16:00 UTC is 18:00 in Rome (CEST): screening 2 starts exactly then.
        var result = await Get(new GetScreeningsQuery(DefaultPage) { From = "2026-06-23T16:00:00Z" });

        Assert.Equal(new[] { 2, 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_from_bound_is_inclusive()
    {
        await SeedGraphAsync();

        // From exactly matches screening 2's datetime — it must be included.
        var result = await Get(new GetScreeningsQuery(DefaultPage) { From = "2026-06-23T18:00:00" });

        Assert.Equal(new[] { 2, 3, 4 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_to_bound_is_inclusive()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage) { To = "2026-06-24T21:00:00" });

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_from_and_to_select_inclusive_range()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage)
        {
            From = "2026-06-23T00:00:00",
            To = "2026-06-24T23:59:59",
        });

        Assert.Equal(new[] { 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_combines_filters()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage)
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

        var result = await Get(new GetScreeningsQuery(DefaultPage) { WithRelations = false });

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

        var result = await Get(new GetScreeningsQuery(DefaultPage) { WithRelations = true });

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
        var result = await Get(new GetScreeningsQuery(PageRequest.Create(2, 1, GetScreeningsHandler.DefaultLimit, GetScreeningsHandler.MaxLimit)));

        Assert.Equal(new[] { 2, 3 }, result.Select(s => s.Id));
    }

    [Fact]
    public async Task GetAsync_returns_empty_when_no_screenings_match()
    {
        await SeedGraphAsync();

        var result = await Get(new GetScreeningsQuery(DefaultPage) { CinemaId = "nonexistent" });

        Assert.Empty(result);
    }
}
