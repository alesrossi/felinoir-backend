using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Felinoir.Infrastructure.Persistence;

/// <summary>
/// EF Core context mapped onto the pre-existing Postgres schema (see SPEC.md).
/// snake_case column/table naming is applied via UseSnakeCaseNamingConvention()
/// in DI; per-property exceptions live in the IEntityTypeConfiguration classes.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Screening> Screenings => Set<Screening>();
    public DbSet<TmdbLookupCache> TmdbLookupCache => Set<TmdbLookupCache>();
    public DbSet<FelixCache> FelixCache => Set<FelixCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
