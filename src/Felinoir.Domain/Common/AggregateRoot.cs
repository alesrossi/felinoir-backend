namespace Felinoir.Domain.Common;

/// <summary>
/// The entry point of a consistency boundary: the only object of an aggregate that outside code
/// holds, loads through a repository and changes through its methods, which keep the aggregate's
/// invariants. It records the <see cref="IDomainEvent"/>s its changes raise until they are taken
/// after the change is saved.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Returns the recorded events and forgets them.</summary>
    public IReadOnlyList<IDomainEvent> TakeDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}
