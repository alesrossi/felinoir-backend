using Felinoir.Domain.Common;

namespace Felinoir.Api.Errors;

/// <summary>
/// The one place where business errors become HTTP: an <see cref="Error"/> is an RFC 7807
/// problem response whose status comes from its <see cref="ErrorKind"/> and whose <c>error</c>
/// member carries its code, as clients have always read it (<c>{ "error": "name_taken" }</c>).
/// </summary>
public static class ErrorResults
{
    public static int StatusCode(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IResult ToProblem(this Error error) =>
        TypedResults.Problem(
            statusCode: StatusCode(error.Kind),
            detail: error.Detail,
            extensions: new Dictionary<string, object?> { [ProblemDetailsSetup.ErrorMember] = error.Code });

    /// <summary>200 with the value, or the error's problem response.</summary>
    public static IResult ToHttp<T>(this Result<T> result) =>
        result.Match(value => TypedResults.Ok(value), ToProblem);
}
