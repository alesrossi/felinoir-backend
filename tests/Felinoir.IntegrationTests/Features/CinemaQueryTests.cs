using Felinoir.Application.Features;
using Felinoir.Application.Features.Cinemas;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Features;

/// <summary>The cinema query handlers against real Postgres.</summary>
[Collection(PostgresCollection.Name)]
public class CinemaQueryTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private async Task<IReadOnlyList<CinemaResponse>> GetCinemas(string? city = null)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetCinemasHandler(db).Handle(new GetCinemasQuery(city), default);
    }

    private async Task<Felinoir.Application.Common.Results.Result<CinemaResponse>> GetById(string id)
    {
        await using var db = Fixture.NewDbContext();
        return await new GetCinemaByIdHandler(db).Handle(new GetCinemaByIdQuery(id), default);
    }

    [Fact]
    public async Task Cinemas_are_ordered_by_name()
    {
        await SeedAsync(
            TestData.Cinema("c1", name: "Quattro Fontane"),
            TestData.Cinema("c2", name: "Farnese"),
            TestData.Cinema("c3", name: "Nuovo Sacher"));

        Assert.Equal(["Farnese", "Nuovo Sacher", "Quattro Fontane"], (await GetCinemas()).Select(c => c.Name));
    }

    [Fact]
    public async Task No_cinemas_is_an_empty_list() => Assert.Empty(await GetCinemas());

    [Fact]
    public async Task A_cinema_carries_its_city_name()
    {
        await SeedAsync(TestData.Cinema("cinema-farnese", name: "Farnese"));

        Assert.Equal("Roma", Assert.Single(await GetCinemas()).City);
    }

    [Fact]
    public async Task A_city_keeps_only_its_cinemas()
    {
        await SeedAsync(TestData.City("milan", "Milano"));
        await SeedAsync(TestData.Cinema("roma-1", name: "A"), TestData.Cinema("milano-1", name: "B", cityId: "milan"));

        Assert.Equal(["roma-1"], (await GetCinemas("Roma")).Select(c => c.Id));
        Assert.Equal(["milano-1"], (await GetCinemas("milan")).Select(c => c.Id));
        Assert.Empty(await GetCinemas("Torino"));
    }

    [Fact]
    public async Task A_cinema_is_found_by_id()
    {
        await SeedAsync(TestData.Cinema("cinema-farnese", name: "Farnese"), TestData.Cinema("cinema-sacher", name: "Nuovo Sacher"));

        Assert.Equal("Nuovo Sacher", (await GetById("cinema-sacher")).Value.Name);
    }

    [Fact]
    public async Task An_unknown_id_is_not_found()
    {
        await SeedAsync(TestData.Cinema("cinema-farnese"));

        Assert.Same(CatalogueErrors.NotFound, (await GetById("cinema-unknown")).Error);
    }
}
