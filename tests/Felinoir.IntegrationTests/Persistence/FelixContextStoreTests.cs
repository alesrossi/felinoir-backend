using Felinoir.Application.Features.Felix;
using Felinoir.Infrastructure.Felix;
using Felinoir.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Felinoir.IntegrationTests.Persistence;

/// <summary>The persisted Felix context of each city (<c>felix_cache</c>), against real Postgres.</summary>
[Collection(PostgresCollection.Name)]
public class FelixContextStoreTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private static readonly DateTimeOffset BuiltAt = new(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);

    private async Task<T> WithStore<T>(Func<FelixContextStore, Task<T>> use)
    {
        await using var db = Fixture.NewDbContext();
        return await use(new FelixContextStore(db, NullLogger<FelixContextStore>.Instance));
    }

    [Fact]
    public async Task A_saved_cache_loads_back_with_its_build_time()
    {
        await WithStore(async s =>
        {
            await s.SaveAsync("rome", FelixContext.Cached("cachedContents/a", BuiltAt.AddHours(24), BuiltAt));
            return 0;
        });

        var loaded = await WithStore(s => s.LoadAsync("rome"));

        Assert.Equal("cachedContents/a", loaded!.CacheName);
        Assert.Equal(BuiltAt.AddHours(24), loaded.CacheExpiresAt);
        Assert.Equal(BuiltAt, loaded.BuiltAt);
    }

    [Fact]
    public async Task Saving_again_replaces_the_citys_row()
    {
        await WithStore(async s =>
        {
            await s.SaveAsync("rome", FelixContext.Cached("cachedContents/a", BuiltAt.AddHours(24), BuiltAt));
            await s.SaveAsync("rome", FelixContext.Cached("cachedContents/b", BuiltAt.AddHours(48), BuiltAt.AddDays(1)));
            return 0;
        });

        await using var db = Fixture.NewDbContext();
        var row = await db.FelixCache.AsNoTracking().SingleAsync();
        Assert.Equal(("rome", "cachedContents/b", "2026-06-24T08:00:00Z", "2026-06-23T08:00:00Z"),
            (row.CityId, row.CacheName, row.ExpiresAt, row.CreatedAt));
    }

    [Fact]
    public async Task Inline_contexts_and_missing_rows_are_not_persisted() =>
        Assert.Null(await WithStore(async s =>
        {
            await s.SaveAsync("rome", FelixContext.Inline("corpus", BuiltAt));
            return await s.LoadAsync("rome");
        }));
}
