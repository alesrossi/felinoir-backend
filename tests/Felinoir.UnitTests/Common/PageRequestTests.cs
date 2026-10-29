using Felinoir.Application.Common;

namespace Felinoir.UnitTests.Common;

public class PageRequestTests
{
    [Theory]
    [InlineData(null, null, 500, 0)]       // defaults
    [InlineData(10, 20, 10, 20)]
    [InlineData(-5, -3, 0, 0)]             // negatives clamp to 0
    [InlineData(99_999, 0, 2000, 0)]       // above the maximum clamps to it
    [InlineData(0, 0, 0, 0)]
    public void Clamps_instead_of_rejecting(int? limit, int? offset, int expectedLimit, int expectedOffset)
    {
        var page = PageRequest.Create(limit, offset, defaultLimit: 500, maxLimit: 2000);

        Assert.Equal(expectedLimit, page.Limit);
        Assert.Equal(expectedOffset, page.Offset);
    }

    [Fact]
    public void Rejects_a_default_above_the_maximum() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PageRequest.Create(null, null, defaultLimit: 10, maxLimit: 5));
}
