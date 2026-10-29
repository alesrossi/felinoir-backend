using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
    DbSet<FelixCache> FelixCache { get; }
    DbSet<Lobby> Lobbies { get; }
    DbSet<LobbyParticipant> LobbyParticipants { get; }

    /// <summary>For explicit transactions when one operation needs several ordered saves.</summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Throws <see cref="Common.Exceptions.UniqueConstraintViolationException"/> when a unique
    /// constraint or index is violated.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
