using Felinoir.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
            // InMemory has no transactions; services that open one still run, just without isolation.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }
}
