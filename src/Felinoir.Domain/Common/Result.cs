using System.Diagnostics.CodeAnalysis;

namespace Felinoir.Domain.Common;

/// <summary>
/// The outcome of an operation that returns nothing: success, or an <see cref="Results.Error"/>.
/// An error converts implicitly, so a handler can <c>return Error.NotFound("not_found");</c>.
/// </summary>
public class Result : IFailureResult<Result>
{
    protected Result(Error? error) => Error = error;

    public static Result Success { get; } = new(null);

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Failure(Error error) => new(error);

    static Result IFailureResult<Result>.Failed(Error error) => Failure(error);

    public static Result<T> Ok<T>(T value) => value;

    public static Result<T> Fail<T>(Error error) => error;

#pragma warning disable CA2225 // Failure is the named alternate.
    public static implicit operator Result(Error error) => Failure(error);
#pragma warning restore CA2225
}

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/>: the value, or an
/// <see cref="Results.Error"/>. Both convert implicitly, so a handler can return either.
/// </summary>
public sealed class Result<T> : Result, IFailureResult<Result<T>>
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error) { }

    /// <summary>The value; throws on a failed result, so check <see cref="Result.IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed result has no value (error: {Error.Code}).");

    static Result<T> IFailureResult<Result<T>>.Failed(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> success, Func<Error, TOut> failure) =>
        IsSuccess ? success(_value!) : failure(Error);

#pragma warning disable CA2225 // Result.Ok / Result.Fail are the named alternates.
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
#pragma warning restore CA2225
}
