namespace Felinoir.Api.Endpoints;

/// <summary>Wire shape of a <c>POST /lobbies</c> body. <see cref="Note"/> is optional.</summary>
public sealed class CreateLobbyRequest
{
    public int? ScreeningId { get; set; }
    public string? Name { get; set; }
    public string? Note { get; set; }
}

/// <summary>Wire shape of a <c>PATCH /lobbies/{id}</c> body.</summary>
public sealed class UpdateLobbyNoteRequest
{
    public string? Note { get; set; }
}

/// <summary>Wire shape of the join and rename bodies.</summary>
public sealed class LobbyParticipantNameRequest
{
    public string? Name { get; set; }
}
