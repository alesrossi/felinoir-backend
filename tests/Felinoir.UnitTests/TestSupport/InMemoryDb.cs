using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.UnitTests.TestSupport;

/// <summary>
/// Creates an isolated <see cref="ApplicationDbContext"/> over the EF Core InMemory
/// provider for service-layer unit tests that need a DbContext but not real Postgres.
/// </summary>
internal static class InMemoryDb
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"felinoir-unit-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
