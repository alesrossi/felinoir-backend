using Felinoir.Domain.Common;
using Felinoir.Domain.Lobbies;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>
/// The shape of every command on an existing lobby: load it (an expired one is not found), apply one
/// change through the aggregate, and answer with the lobby as the calling device now sees it. The
/// unit of work saves the change after the handler returns.
/// </summary>
public abstract class LobbyCommandHandler(ILobbyRepository lobbies, LobbyViews views, TimeProvider time)
{
    protected async ValueTask<Result<LobbyView>> ChangeAsync(
        LobbyId lobbyId, string deviceToken, Func<Lobby, DeviceHash, Result> change, CancellationToken cancellationToken)
    {
        var lobby = await lobbies.GetAsync(lobbyId, cancellationToken);
        if (lobby is null || lobby.IsExpiredAt(time.GetUtcNow())) return LobbyErrors.NotFound;

        var device = DeviceHash.FromToken(deviceToken);
        var changed = change(lobby, device);
        if (!changed.IsSuccess) return changed.Error;

        return await views.ForAsync(lobby, device, cancellationToken);
    }
}
