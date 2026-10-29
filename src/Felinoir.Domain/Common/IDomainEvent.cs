namespace Felinoir.Domain.Common;

/// <summary>Something that happened inside an aggregate that other parts of the system may react to.</summary>
#pragma warning disable CA1040 // A marker interface: domain events have no common members.
public interface IDomainEvent;
#pragma warning restore CA1040
