using Felinoir.Application.Common.Exceptions;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Felinoir.Infrastructure.Persistence.ScraperTables;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Reflection;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// EF Core context mapped onto the pre-existing Postgres schema (see SPEC.md).
/// snake_case column/table naming is applied via UseSnakeCaseNamingConvention()
/// in DI; per-property exceptions live in the IEntityTypeConfiguration classes.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext, IUnitOfWork, IReadDbContext
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
    public DbSet<LobbyParticipant> LobbyParticipants => Set<LobbyParticipant>();

    // The read side: untracked, so nothing read through IReadDbContext can be saved.
    IQueryable<City> IReadDbContext.Cities => Cities.AsNoTracking();
    IQueryable<Cinema> IReadDbContext.Cinemas => Cinemas.AsNoTracking();
    IQueryable<Movie> IReadDbContext.Movies => Movies.AsNoTracking();
    IQueryable<Screening> IReadDbContext.Screenings => Screenings.AsNoTracking();
    IQueryable<Lobby> IReadDbContext.Lobbies => Lobbies.AsNoTracking();
    IQueryable<LobbyParticipant> IReadDbContext.LobbyParticipants => LobbyParticipants.AsNoTracking();
    IQueryable<FelixCache> IReadDbContext.FelixCache => FelixCache.AsNoTracking();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Surface the constraint name without leaking Npgsql into the Application layer.
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram matching for the scrapers' title dedup (idx_movies_canonical_trgm).
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
