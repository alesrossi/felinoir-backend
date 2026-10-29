using Felinoir.Domain.Common;

namespace Felinoir.UnitTests.Domain;

public class AggregateRootTests
{
    private sealed record Renamed(string Name) : IDomainEvent;

    private sealed class Thing(int id) : AggregateRoot<int>(id)
    {
        public void Rename(string name) => Raise(new Renamed(name));
    }

    private sealed class OtherThing(int id) : Entity<int>(id);

    [Fact]
    public void Entities_are_equal_by_type_and_id()
    {
        Assert.Equal(new Thing(1), new Thing(1));
        Assert.True(new Thing(1) == new Thing(1));
        Assert.NotEqual(new Thing(1), new Thing(2));
        Assert.False(new Thing(1).Equals(new OtherThing(1)));
        Assert.Equal(new Thing(1).GetHashCode(), new Thing(1).GetHashCode());
    }

    [Fact]
    public void Raised_events_are_recorded_until_taken()
    {
        var thing = new Thing(1);
        thing.Rename("a");
        thing.Rename("b");

        var events = thing.TakeDomainEvents();

        Assert.Equal([new Renamed("a"), new Renamed("b")], events);
        Assert.Empty(thing.DomainEvents);
        Assert.Empty(thing.TakeDomainEvents());
    }
}
