using Felinoir.Domain.Common;

namespace Felinoir.UnitTests.Domain;

public class AggregateRootTests
{
    private sealed class Thing(int id) : AggregateRoot<int>(id);

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
}
