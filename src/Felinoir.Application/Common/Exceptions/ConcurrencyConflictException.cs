namespace Felinoir.Application.Common.Exceptions;

/// <summary>
/// A save lost a race: the aggregate changed in the database since it was loaded (its concurrency
/// token no longer matched). Raised by the persistence layer in place of EF's own exception.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data changed while the request was being handled.", innerException);
