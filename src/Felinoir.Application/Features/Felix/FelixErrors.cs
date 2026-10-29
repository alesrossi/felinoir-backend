using Felinoir.Domain.Common;

namespace Felinoir.Application.Features.Felix;

/// <summary>
/// Felix's errors. The frontend shows <c>error</c> to the user as is, so their codes are the
/// Italian messages Felix has always answered with.
/// </summary>
public static class FelixErrors
{
    public static readonly Error InvalidRequest = Error.Validation("Richiesta non valida.");

    public static readonly Error NotConfigured = Error.Unavailable("Felix non è configurato al momento.");

    public static readonly Error Failed =
        new("Qualcosa è andato storto. Riprova tra qualche secondo.", ErrorKind.Unexpected);
}

/// <summary><c>{ "reply": "..." }</c>.</summary>
public sealed record FelixReply(string Reply);
