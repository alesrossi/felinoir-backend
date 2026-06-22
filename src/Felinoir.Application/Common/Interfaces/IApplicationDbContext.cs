using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core context exposed to the Application layer.
/// Services query these <see cref="DbSet{T}"/>s directly with LINQ — this is the
/// EF Core unit of work itself, deliberately *not* wrapped in a repository pattern.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Cinema> Cinemas { get; }
    DbSet<Movie> Movies { get; }
    DbSet<Screening> Screenings { get; }
    DbSet<TmdbLookupCache> TmdbLookupCache { get; }
    DbSet<FelixCache> FelixCache { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
