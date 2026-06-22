using Felinoir.Application.Cinemas;
using Felinoir.IntegrationTests.Support;

namespace Felinoir.IntegrationTests.Services;

[Collection(PostgresCollection.Name)]
public class CinemaServiceTests : PostgresTestBase
{
    public CinemaServiceTests(PostgresFixture fixture) : base(fixture) { }

    private CinemaService NewService() => new(Fixture.NewDbContext());

    [Fact]
    public async Task GetAllAsync_orders_by_name_ascending()
    {
        await SeedAsync(
            TestData.Cinema("c1", name: "Quattro Fontane"),
            TestData.Cinema("c2", name: "Farnese"),
            TestData.Cinema("c3", name: "Nuovo Sacher"));

        var result = await NewService().GetAllAsync();

        Assert.Equal(new[] { "Farnese", "Nuovo Sacher", "Quattro Fontane" }, result.Select(c => c.Name));
    }

    [Fact]
    public async Task GetAllAsync_returns_empty_when_none()
    {
        var result = await NewService().GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_returns_matching_cinema()
    {
        await SeedAsync(
            TestData.Cinema("cinema-farnese", name: "Farnese"),
            TestData.Cinema("cinema-sacher", name: "Nuovo Sacher"));

        var result = await NewService().GetByIdAsync("cinema-sacher");

        Assert.NotNull(result);
        Assert.Equal("Nuovo Sacher", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_not_found()
    {
        await SeedAsync(TestData.Cinema("cinema-farnese"));

        var result = await NewService().GetByIdAsync("cinema-unknown");

        Assert.Null(result);
    }
}
