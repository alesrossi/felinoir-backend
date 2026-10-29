using Felinoir.Application.Common.Results;

namespace Felinoir.Application.Features;

/// <summary>
/// Errors of the read-only catalogue (movies, cinemas, screenings). Their codes are the texts
/// these endpoints have always returned in <c>error</c>, kept for compatibility.
/// </summary>
public static class CatalogueErrors
{
    public static readonly Error NotFound = Error.NotFound("Not found");
    public static readonly Error InvalidId = Error.Validation("Invalid id");
}
