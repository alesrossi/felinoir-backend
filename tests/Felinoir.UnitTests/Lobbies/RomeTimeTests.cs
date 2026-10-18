using Felinoir.Application.Lobbies;

namespace Felinoir.UnitTests.Lobbies;

public class RomeTimeTests
{
    [Theory]
    [InlineData("2026-10-17T21:00:00", "2026-10-17T19:00:00Z")]   // CEST, UTC+2
    [InlineData("2026-12-01T21:00:00", "2026-12-01T20:00:00Z")]   // CET, UTC+1
    [InlineData("2026-10-17T21:00:00Z", "2026-10-17T21:00:00Z")]  // explicit UTC kept as is
    [InlineData("2026-10-17T21:00:00+01:00", "2026-10-17T20:00:00Z")]
    public void TryParseWallClock_reads_screening_times_as_rome(string input, string expectedUtc)
    {
        Assert.True(RomeTime.TryParseWallClock(input, out var utc));
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(expectedUtc, RomeTime.ToUtcString(utc));
    }

    [Fact]
    public void TryParseWallClock_rejects_garbage() =>
        Assert.False(RomeTime.TryParseWallClock("not a date", out _));

    [Theory]
    [InlineData("2026-10-17T21:00:00")]
    [InlineData("2026-12-01T09:30:00")]
    [InlineData("2026-03-29T03:00:00")]  // first hour after the spring-forward jump
    public void ToWallClock_round_trips_the_screening_format(string wallClock)
    {
        RomeTime.TryParseWallClock(wallClock, out var utc);

        Assert.Equal(wallClock, RomeTime.ToWallClock(utc));
    }
}
