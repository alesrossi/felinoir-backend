using Felinoir.Application.Common.Exceptions;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Felinoir.Domain.Lobbies;
using Felinoir.Infrastructure.Persistence.ScraperTables;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// The EF Core context, whose model and migrations own the database schema (ADR 0001). One per
/// request, behind both sides of ADR 0002: <see cref="IReadDbContext"/> (untracked queries) and
/// <see cref="IUnitOfWork"/> (committing what repositories loaded and changed). snake_case naming
/// comes from UseSnakeCaseNamingConvention(); per-property mappings live in the
/// IEntityTypeConfiguration classes.
/// </summary>
public class ApplicationDbContext : DbContext, IUnitOfWork, IReadDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<City> Cities => Set<City>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Screening> Screenings => Set<Screening>();
    public DbSet<TmdbLookupCache> TmdbLookupCache => Set<TmdbLookupCache>();
    public DbSet<AiMatchVerdict> AiMatchVerdicts => Set<AiMatchVerdict>();
    public DbSet<FelixCache> FelixCache => Set<FelixCache>();
    public DbSet<Lobby> Lobbies => Set<Lobby>();

    // The read side: untracked, so nothing read through IReadDbContext can be saved.
    IQueryable<City> IReadDbContext.Cities => Cities.AsNoTracking();
    IQueryable<Cinema> IReadDbContext.Cinemas => Cinemas.AsNoTracking();
    IQueryable<Movie> IReadDbContext.Movies => Movies.AsNoTracking();
    IQueryable<Screening> IReadDbContext.Screenings => Screenings.AsNoTracking();
    IQueryable<Lobby> IReadDbContext.Lobbies => Lobbies.AsNoTracking();
    IQueryable<FelixCache> IReadDbContext.FelixCache => FelixCache.AsNoTracking();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (Exception ex) when (UniqueViolation(ex) is { } pg)
        {
            // Surface the constraint name without leaking Npgsql into the Application layer. A
            // deferred constraint fails at commit, so the error isn't always a DbUpdateException.
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    public void DiscardChanges() => ChangeTracker.Clear();

    private static PostgresException? UniqueViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg) return pg;
        return null;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram matching for the scrapers' title dedup (idx_movies_canonical_trgm).
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
