namespace Felinoir.Application.Common.Interfaces;

/// <summary>
/// Commits every change the current request made to its aggregates, in one transaction. Called by
/// <see cref="Behaviors.UnitOfWorkBehavior{TMessage, TResponse}"/>, not by handlers.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Throws <see cref="Exceptions.UniqueConstraintViolationException"/> when a unique constraint
    /// or index is violated.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
