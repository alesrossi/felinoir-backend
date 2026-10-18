using Microsoft.EntityFrameworkCore;

namespace Felinoir.Application.Common.Exceptions;

/// <summary>
/// A save hit a unique constraint or unique index. Raised by the persistence layer in place
/// of the provider's own exception, so services can map <see cref="ConstraintName"/> to a
/// business error without depending on the database driver.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : DbUpdateException($"Unique constraint '{constraintName}' violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
