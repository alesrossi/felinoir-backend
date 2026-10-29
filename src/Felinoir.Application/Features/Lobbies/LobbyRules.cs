using Felinoir.Domain.Lobbies;
using FluentValidation;

namespace Felinoir.Application.Features.Lobbies;

/// <summary>The request-shape rules shared by the lobby commands; the codes are the API's error codes.</summary>
internal static class LobbyRules
{
    public const string MissingDevice = "missing_device";

    /// <summary>Every lobby request comes from a device (the frontend's <c>X-Device-Token</c>).</summary>
    public static IRuleBuilderOptions<T, string> IsDeviceToken<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode(MissingDevice).WithMessage("The device token is missing.");

    public static IRuleBuilderOptions<T, string?> IsParticipantName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(ParticipantName.IsValid)
            .WithErrorCode(LobbyErrors.InvalidName.Code)
            .WithMessage($"A name is 1 to {ParticipantName.MaxLength} characters, without control characters.");

    public static IRuleBuilderOptions<T, string?> IsLobbyNote<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(LobbyNote.IsValid)
            .WithErrorCode(LobbyErrors.InvalidNote.Code)
            .WithMessage($"A note is at most {LobbyNote.MaxLength} characters.");
}
