using Felinoir.Domain.Common;
using FluentValidation;
using Mediator;

namespace Felinoir.Application.Common.Behaviors;

/// <summary>
/// Runs the request's FluentValidation validators before its handler. A request that fails comes
/// back as a validation <see cref="Error"/> with the first failure's error code (validators set it
/// with <c>WithErrorCode</c>, e.g. <c>invalid_name</c>) and every failure's message as the detail;
/// the handler isn't called. Validators check the shape of the request; business rules belong to
/// the domain.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
    where TResponse : IFailureResult<TResponse>
{
    private readonly IValidator<TMessage>[] _validators = validators.ToArray();

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Length == 0) return await next(message, cancellationToken);

        // A context per validator: FluentValidation accumulates failures on the context, so a
        // shared one would report each earlier validator's failures again.
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
            failures.AddRange((await validator.ValidateAsync(message, cancellationToken)).Errors);

        if (failures.Count == 0) return await next(message, cancellationToken);

        return TResponse.Failed(Error.Validation(
            failures[0].ErrorCode,
            string.Join(" ", failures.Select(f => f.ErrorMessage))));
    }
}
