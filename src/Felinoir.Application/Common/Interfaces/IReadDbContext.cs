using Felinoir.Domain.Entities;
using Felinoir.Domain.Lobbies;

namespace Felinoir.Application.Common.Interfaces;

/// <summary>
/// The read side (ADR 0002): queries project straight from these sets into response records.
/// Nothing read here is tracked, so nothing read here can be saved; changes go through
/// aggregates, repositories and <see cref="IUnitOfWork"/>.
/// </summary>
public interface IReadDbContext
{
    IQueryable<City> Cities { get; }
    IQueryable<Cinema> Cinemas { get; }
    IQueryable<Movie> Movies { get; }
    IQueryable<Screening> Screenings { get; }
    IQueryable<Lobby> Lobbies { get; }
    IQueryable<FelixCache> FelixCache { get; }
}
