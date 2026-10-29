using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Common.Exceptions;

/// <summary>
/// A save hit a unique constraint or unique index (possibly a deferred one, at commit). Raised by
/// the persistence layer in place of the provider's own exception, so the application can react
/// without depending on the database driver.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : DbUpdateException($"Unique constraint '{constraintName}' violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
