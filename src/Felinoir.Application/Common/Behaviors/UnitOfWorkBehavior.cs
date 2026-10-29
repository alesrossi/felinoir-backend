using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Common.Results;
using Mediator;

namespace Felinoir.Application.Common.Behaviors;

/// <summary>
/// Last before a command's handler: once the handler has changed its aggregates, commits them in
/// one <see cref="IUnitOfWork.SaveChangesAsync"/>. A failed <see cref="Result"/> commits nothing,
/// so a handler can return an error after changing an aggregate without half-saving it. Queries
/// don't pass through here.
/// </summary>
public sealed class UnitOfWorkBehavior<TMessage, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);

        if (response is not Result { IsSuccess: false })
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
