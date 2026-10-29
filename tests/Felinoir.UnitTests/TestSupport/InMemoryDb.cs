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

        var db = new ApplicationDbContext(options);

        // InMemory doesn't apply column defaults. movies.has_matches is the one required column
        // with only a database default (a shadow property no test sets), so fill it here.
        db.SavingChanges += (_, _) =>
        {
            foreach (var entry in db.ChangeTracker.Entries<Domain.Entities.Movie>())
            {
                var hasMatches = entry.Property("HasMatches");
                if (entry.State == EntityState.Added && hasMatches.CurrentValue is null)
                    hasMatches.CurrentValue = "FailedMatch";
            }
        };

        return db;
    }
}
