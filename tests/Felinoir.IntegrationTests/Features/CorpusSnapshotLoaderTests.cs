using Felinoir.Application.Features.Felix;
using Felinoir.Domain.Entities;
using Felinoir.IntegrationTests.Contract;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Features;

/// <summary>What goes into a city's Felix corpus, against real Postgres.</summary>
[Collection(PostgresCollection.Name)]
public class CorpusSnapshotLoaderTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    /// <summary>2026-06-22 20:30 in Rome (CEST, UTC+2).</summary>
    private static readonly DateTimeOffset Now = new(2026, 6, 22, 18, 30, 0, TimeSpan.Zero);

    private async Task<CorpusSnapshot> LoadAsync(string cityId = City.RomeId)
    {
        await using var db = Fixture.NewDbContext();
        return await new CorpusSnapshotLoader(db, new MovableTimeProvider(Now)).LoadAsync(cityId);
    }

    [Fact]
    public async Task Upcoming_means_not_started_in_italian_time()
    {
        // At 20:30 in Rome, 20:00 has started and 21:00 hasn't. Compared with the UTC clock
        // (18:30), the 20:00 screening used to count as upcoming.
        await SeedAsync(TestData.Cinema("c"), TestData.Movie(1), TestData.Movie(2), TestData.Movie(3));
        await SeedAsync(
            TestData.Screening(1, 1, "c", "2026-06-22T20:00:00"),
            TestData.Screening(2, 2, "c", "2026-06-22T21:00:00"),
            TestData.Screening(3, 3, "c", "2026-06-20T21:00:00"));

        var snapshot = await LoadAsync();

        Assert.Equal([2], snapshot.UpcomingScreenings.Select(s => s.Id));
        Assert.Equal("2026-06-22T20:30:00", snapshot.Now.ToString());
    }

    [Fact]
    public async Task A_snapshot_covers_only_its_city()
    {
        await SeedAsync(TestData.City("milan", "Milano"));
        await SeedAsync(
            TestData.Cinema("roma-1", name: "Cine Romano"),
            TestData.Cinema("milano-1", name: "Cinema Milanese", cityId: "milan"),
            TestData.Movie(1), TestData.Movie(2));
        await SeedAsync(
            TestData.Screening(1, 1, "roma-1", "2026-06-24T21:00:00"),
            TestData.Screening(2, 2, "milano-1", "2026-06-24T21:00:00"));

        var rome = await LoadAsync();
        var milan = await LoadAsync("milan");

        Assert.Equal("Roma", rome.CityName);
        Assert.Equal(["roma-1"], rome.Cinemas.Select(c => c.Id));
        Assert.Equal([1], rome.UpcomingScreenings.Select(s => s.Id));
        Assert.Equal("Milano", milan.CityName);
        Assert.Equal([2], milan.UpcomingScreenings.Select(s => s.Id));
        Assert.Equal([1, 2], milan.Movies.Select(m => m.Id)); // the catalogue is national
    }
}
