namespace Felinoir.Domain.Common;

/// <summary>What kind of failure an <see cref="Error"/> is; the API maps each kind to one HTTP status.</summary>
public enum ErrorKind
{
    /// <summary>The request itself is invalid (400).</summary>
    Validation,

    /// <summary>What the request refers to doesn't exist (404).</summary>
    NotFound,

    /// <summary>The request clashes with the current state (409).</summary>
    Conflict,

    /// <summary>Well-formed, but can't be carried out, e.g. a screening that already started (422).</summary>
    Unprocessable,

    /// <summary>A dependency the request needs isn't available (503).</summary>
    Unavailable,

    /// <summary>Something failed that the caller can only retry later (500), reported with a message rather than thrown.</summary>
    Unexpected,
}

/// <summary>
/// A business failure, returned (not thrown) by aggregates and handlers. <see cref="Code"/> is the stable,
/// machine-readable code clients switch on (the API's <c>error</c> field, e.g. <c>name_taken</c>);
/// <see cref="Detail"/> is optional human-readable text.
/// </summary>
public sealed record Error(string Code, ErrorKind Kind, string? Detail = null)
{
    public static Error Validation(string code, string? detail = null) => new(code, ErrorKind.Validation, detail);
    public static Error NotFound(string code, string? detail = null) => new(code, ErrorKind.NotFound, detail);
    public static Error Conflict(string code, string? detail = null) => new(code, ErrorKind.Conflict, detail);
    public static Error Unprocessable(string code, string? detail = null) => new(code, ErrorKind.Unprocessable, detail);
    public static Error Unavailable(string code, string? detail = null) => new(code, ErrorKind.Unavailable, detail);
}
