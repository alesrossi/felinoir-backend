using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>
/// A lobby as the calling device sees it. With <see cref="IfVersion"/>, nothing is loaded when the
/// lobby is still at that version (<see cref="LobbyPoll.NotModified"/>): pages poll this once a second.
/// </summary>
public sealed record GetLobbyQuery(LobbyId LobbyId, string DeviceToken, long? IfVersion = null)
    : IQuery<Result<LobbyPoll>>;

/// <summary>The lobby, or nothing when it hasn't changed since the version the caller has.</summary>
public sealed record LobbyPoll(LobbyView? Lobby)
{
    public static readonly LobbyPoll Unchanged = new((LobbyView?)null);

    public bool NotModified => Lobby is null;
}

public sealed class GetLobbyHandler(IReadDbContext db, LobbyViews views, TimeProvider time)
    : IQueryHandler<GetLobbyQuery, Result<LobbyPoll>>
{
    public async ValueTask<Result<LobbyPoll>> Handle(GetLobbyQuery query, CancellationToken cancellationToken)
    {
        // The common "nothing changed" answer is a primary-key lookup of two columns.
        var head = await db.Lobbies
            .Where(l => l.Id == query.LobbyId)
            .Select(l => new { l.Version, l.StartsAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (head is null || head.StartsAt + Lobby.Lifetime < time.GetUtcNow()) return LobbyErrors.NotFound;
        if (head.Version == query.IfVersion) return LobbyPoll.Unchanged;

        var lobby = await db.Lobbies
            .Include(l => l.Participants)
            .FirstOrDefaultAsync(l => l.Id == query.LobbyId, cancellationToken);
        if (lobby is null) return LobbyErrors.NotFound;

        return new LobbyPoll(await views.ForAsync(lobby, DeviceHash.FromToken(query.DeviceToken), cancellationToken));
    }
}
