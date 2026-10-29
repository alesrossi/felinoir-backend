using Felinoir.Domain.Lobbies;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>
/// Loads and adds <see cref="Lobby"/> aggregates, participants included. Changes are saved by the
/// unit of work at the end of the command; removing a lobby is the database's expiry job.
/// </summary>
public interface ILobbyRepository
{
    Task<Lobby?> GetAsync(LobbyId id, CancellationToken cancellationToken = default);

    void Add(Lobby lobby);
}
