namespace Felinoir.Domain.Common;

/// <summary>
/// The entry point of a consistency boundary: the only object of an aggregate that outside code
/// holds, loads through a repository and changes through its methods, which keep the aggregate's
/// invariants.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }
}
