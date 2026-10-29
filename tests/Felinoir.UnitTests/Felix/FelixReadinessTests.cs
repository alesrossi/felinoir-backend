using Felinoir.Application.Features.Felix;

namespace Felinoir.UnitTests.Felix;

/// <summary>When a Felix context may be used, as pure functions: no fakes, no clock.</summary>
public class FelixReadinessTests
{
    private static readonly TimeOnly FiveAm = new(5, 0);

    /// <summary>2026-06-22 10:00 in Rome (CEST): today's 05:00 rebuild time is 03:00Z.</summary>
    private static readonly DateTimeOffset Morning = new(2026, 6, 22, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset TodaysRebuild = new(2026, 6, 22, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_latest_rebuild_time_is_todays_once_it_has_passed_else_yesterdays()
    {
        Assert.Equal(TodaysRebuild, FelixReadiness.LatestRebuildTime(Morning, FiveAm));
        Assert.Equal(TodaysRebuild, FelixReadiness.LatestRebuildTime(TodaysRebuild, FiveAm));
        Assert.Equal(TodaysRebuild.AddDays(-1), FelixReadiness.LatestRebuildTime(TodaysRebuild.AddSeconds(-1), FiveAm));
    }

    [Fact]
    public void The_rebuild_time_follows_italian_clocks_in_winter()
    {
        var winterMorning = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero); // 10:00 CET

        Assert.Equal(new DateTimeOffset(2026, 12, 1, 4, 0, 0, TimeSpan.Zero), FelixReadiness.LatestRebuildTime(winterMorning, FiveAm));
    }

    [Fact]
    public void A_cache_built_today_is_fresh_until_an_hour_before_it_expires()
    {
        var cache = FelixContext.Cached("cachedContents/a", Morning.AddHours(5), builtAt: TodaysRebuild.AddMinutes(1));

        Assert.True(FelixReadiness.IsFresh(cache, Morning, FiveAm));
        Assert.True(FelixReadiness.IsFresh(cache, Morning.AddHours(3).AddMinutes(59), FiveAm));
        Assert.False(FelixReadiness.IsFresh(cache, Morning.AddHours(4), FiveAm));
    }

    [Fact]
    public void A_context_built_before_the_latest_rebuild_time_is_stale_however_long_it_would_last()
    {
        // Built yesterday evening, before last night's scrape.
        var builtYesterday = TodaysRebuild.AddHours(-10);

        Assert.False(FelixReadiness.IsFresh(FelixContext.Cached("cachedContents/a", Morning.AddHours(20), builtYesterday), Morning, FiveAm));
        Assert.False(FelixReadiness.IsFresh(FelixContext.Inline("corpus", builtYesterday), Morning, FiveAm));
    }

    [Fact]
    public void An_inline_corpus_stays_fresh_until_the_next_rebuild_time()
    {
        // Built a minute after today's rebuild time: fresh until a minute before tomorrow's. Any
        // 24 h window contains a rebuild time, so the 24 h inline limit is only a safety net.
        var inline = FelixContext.Inline("corpus", builtAt: TodaysRebuild.AddMinutes(1));

        Assert.True(FelixReadiness.IsFresh(inline, TodaysRebuild.AddDays(1).AddMinutes(-1), FiveAm));
        Assert.False(FelixReadiness.IsFresh(inline, TodaysRebuild.AddDays(1), FiveAm));
    }

    [Fact]
    public void Decide_uses_a_fresh_context_then_looks_at_the_persisted_one_then_rebuilds()
    {
        var fresh = FelixContext.Inline("corpus", Morning);
        var stale = FelixContext.Inline("corpus", TodaysRebuild.AddHours(-1));

        Assert.Equal(FelixReadinessAction.UseCurrent, FelixReadiness.Decide(fresh, persistedLoaded: false, Morning, FiveAm));
        Assert.Equal(FelixReadinessAction.LoadPersisted, FelixReadiness.Decide(stale, persistedLoaded: false, Morning, FiveAm));
        Assert.Equal(FelixReadinessAction.LoadPersisted, FelixReadiness.Decide(null, persistedLoaded: false, Morning, FiveAm));
        Assert.Equal(FelixReadinessAction.Rebuild, FelixReadiness.Decide(stale, persistedLoaded: true, Morning, FiveAm));
        Assert.Equal(FelixReadinessAction.Rebuild, FelixReadiness.Decide(null, persistedLoaded: true, Morning, FiveAm));
    }
}
