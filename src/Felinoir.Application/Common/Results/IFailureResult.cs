namespace Felinoir.Application.Common.Results;

/// <summary>
/// A result type that can be created from an <see cref="Error"/>, so a pipeline behaviour can
/// answer with a failure of whatever result type the request declares, without reflection.
/// </summary>
#pragma warning disable CA1000 // Static abstract members are the point of this interface.
public interface IFailureResult<out TSelf>
    where TSelf : IFailureResult<TSelf>
{
    static abstract TSelf Failed(Error error);
}
#pragma warning restore CA1000
