using Felinoir.Application.Common.Exceptions;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Domain.Common;
using Mediator;

namespace Felinoir.Application.Common.Behaviors;

/// <summary>
/// Around <see cref="UnitOfWorkBehavior{TMessage, TResponse}"/>: when a command's commit loses a race
/// (another request changed the same aggregate, or took the same unique value), the command runs
/// once more on fresh data, so the domain gives the real answer (e.g. "name taken"). If the retry
/// loses too, the command fails with a <c>conflict</c> error instead of throwing.
/// </summary>
public sealed class ConflictRetryBehavior<TMessage, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IBaseCommand
    where TResponse : IFailureResult<TResponse>
{
    public static readonly Error Conflict =
        Error.Conflict("conflict", "Someone else changed this at the same time. Try again.");

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception ex) when (IsLostRace(ex))
        {
            unitOfWork.DiscardChanges();
        }

        try
        {
            return await next(message, cancellationToken);
        }
        catch (Exception ex) when (IsLostRace(ex))
        {
            unitOfWork.DiscardChanges();
            return TResponse.Failed(Conflict);
        }
    }

    private static bool IsLostRace(Exception ex) =>
        ex is ConcurrencyConflictException or UniqueConstraintViolationException;
}
