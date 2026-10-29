using Felinoir.Application.Common;
using Felinoir.Domain.ValueObjects;

namespace Felinoir.UnitTests.Domain;

public class ScreeningTimeTests
{
    [Theory]
    [InlineData("2026-10-17T21:00:00", "2026-10-17T19:00:00Z")]   // CEST, UTC+2
    [InlineData("2026-12-01T21:00:00", "2026-12-01T20:00:00Z")]   // CET, UTC+1
    [InlineData("2026-10-17T21:00", "2026-10-17T19:00:00Z")]      // minutes only, as one scraper writes
    public void Reads_stored_times_as_italian_wall_clock(string stored, string expectedUtc)
    {
        Assert.True(ScreeningTime.TryParse(stored, out var time));

        Assert.Equal(expectedUtc, UtcText.Format(time.ToInstant()));
    }

    [Theory]
    [InlineData("2026-10-17T21:00:00Z")]
    [InlineData("2026-10-17T21:00:00+01:00")]
    [InlineData("2026-10-17 21:00:00")]
    [InlineData("2026-10-17")]
    [InlineData("not a date")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_anything_the_scrapers_dont_store(string? stored) =>
        Assert.False(ScreeningTime.TryParse(stored, out _));

    [Theory]
    [InlineData("2026-10-17T21:00:00")]
    [InlineData("2026-12-01T09:30:00")]
    [InlineData("2026-03-29T03:00:00")]  // first hour after the spring-forward jump
    public void Round_trips_through_an_instant(string stored)
    {
        ScreeningTime.TryParse(stored, out var time);

        Assert.Equal(stored, ScreeningTime.FromInstant(time.ToInstant()).ToString());
    }

    [Fact]
    public void Writes_the_canonical_form_with_seconds()
    {
        ScreeningTime.TryParse("2026-10-17T21:00", out var time);

        Assert.Equal("2026-10-17T21:00:00", time.ToString());
    }

    [Fact]
    public void Uses_the_winter_offset_in_the_autumn_hour_that_happens_twice()
    {
        // 02:30 on 2026-10-25 happens at 00:30Z (CEST) and again at 01:30Z (CET).
        ScreeningTime.TryParse("2026-10-25T02:30:00", out var time);

        Assert.Equal("2026-10-25T01:30:00Z", UtcText.Format(time.ToInstant()));
    }

    [Theory]
    [InlineData("2026-06-23T18:00:00", "2026-06-23T18:00:00")]     // no offset: already Italian
    [InlineData("2026-06-23T16:00:00Z", "2026-06-23T18:00:00")]    // UTC, converted (CEST)
    [InlineData("2026-12-23T16:00:00Z", "2026-12-23T17:00:00")]    // UTC, converted (CET)
    [InlineData("2026-06-23T17:00:00+01:00", "2026-06-23T18:00:00")]
    [InlineData("2026-06-23", "2026-06-23T00:00:00")]
    public void Reads_filter_bounds_in_italian_time(string bound, string expected)
    {
        Assert.True(ScreeningTime.TryParseBound(bound, out var time));

        Assert.Equal(expected, time.ToString());
    }

    [Fact]
    public void Rejects_a_bound_that_is_not_a_date() =>
        Assert.False(ScreeningTime.TryParseBound("tomorrow", out _));

    [Fact]
    public void Orders_chronologically()
    {
        ScreeningTime.TryParse("2026-06-23T09:00:00", out var morning);
        ScreeningTime.TryParse("2026-06-23T21:00", out var evening);

        Assert.True(morning < evening);
        Assert.True(evening >= morning);
    }
}
