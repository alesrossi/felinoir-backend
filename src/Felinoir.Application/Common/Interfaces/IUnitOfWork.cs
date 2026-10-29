namespace Felinoir.Application.Common.Interfaces;

/// <summary>
/// Commits every change the current request made to its aggregates, in one transaction. Called by
/// <see cref="Behaviors.UnitOfWorkBehavior{TMessage, TResponse}"/>, not by handlers.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Throws <see cref="Exceptions.ConcurrencyConflictException"/> when an aggregate changed since it
    /// was loaded, and <see cref="Exceptions.UniqueConstraintViolationException"/> when a unique
    /// constraint or index is violated.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Forgets every loaded and changed aggregate, so the request can start over on fresh data.</summary>
    void DiscardChanges();
}
