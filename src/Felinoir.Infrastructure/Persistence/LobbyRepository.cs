using Felinoir.Application.Features.Lobbies;
using Felinoir.Domain.Lobbies;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Infrastructure.Persistence;

public sealed class LobbyRepository(ApplicationDbContext db) : ILobbyRepository
{
    public Task<Lobby?> GetAsync(LobbyId id, CancellationToken cancellationToken = default) =>
        db.Lobbies.Include(l => l.Participants).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public void Add(Lobby lobby) => db.Lobbies.Add(lobby);
}
