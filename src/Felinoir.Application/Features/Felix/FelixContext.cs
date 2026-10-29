namespace Felinoir.Application.Features.Felix;

/// <summary>
/// The corpus currently serving a city, together with the copy of it cached at Gemini (see
/// CONTEXT.md): either a context cache (<see cref="CacheName"/>) or, when Gemini wouldn't cache it,
/// the corpus itself, sent inline with every question. Immutable; a rebuild replaces it.
/// </summary>
public sealed record FelixContext
{
    private FelixContext() { }

    /// <summary>The Gemini context cache, e.g. <c>cachedContents/abc123</c>; null for an inline context.</summary>
    public string? CacheName { get; private init; }

    public DateTimeOffset? CacheExpiresAt { get; private init; }

    /// <summary>The corpus, kept for an inline context.</summary>
    public string? InlineCorpus { get; private init; }

    /// <summary>When the corpus was built from the database.</summary>
    public DateTimeOffset BuiltAt { get; private init; }

    public bool IsCached => CacheName is not null;

    public static FelixContext Cached(string cacheName, DateTimeOffset expiresAt, DateTimeOffset builtAt) =>
        new() { CacheName = cacheName, CacheExpiresAt = expiresAt, BuiltAt = builtAt };

    public static FelixContext Inline(string corpus, DateTimeOffset builtAt) =>
        new() { InlineCorpus = corpus, BuiltAt = builtAt };
}

/// <summary>What to do before answering a question.</summary>
public enum FelixReadinessAction
{
    /// <summary>The context at hand is fresh: answer with it.</summary>
    UseCurrent,

    /// <summary>Nothing fresh in memory: look at the context persisted by an earlier process.</summary>
    LoadPersisted,

    /// <summary>Nothing fresh anywhere: build the corpus again.</summary>
    Rebuild,
}

/// <summary>
/// When a <see cref="FelixContext"/> can still be used (rework plan, Q8/Q12), as pure functions of
/// the context and the time. A context is fresh when it was built after the latest daily rebuild
/// time (so each day is answered from that night's scrape) and, besides, a cache has more than
/// <see cref="CacheLead"/> left or an inline corpus is younger than <see cref="InlineTtl"/>.
/// </summary>
public static class FelixReadiness
{
    public static readonly TimeSpan CacheLead = TimeSpan.FromHours(1);
    public static readonly TimeSpan InlineTtl = TimeSpan.FromHours(24);

    private static readonly TimeZoneInfo Italy = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static FelixReadinessAction Decide(
        FelixContext? current, bool persistedLoaded, DateTimeOffset now, TimeOnly dailyRebuildAt) =>
        current is not null && IsFresh(current, now, dailyRebuildAt) ? FelixReadinessAction.UseCurrent
        : !persistedLoaded ? FelixReadinessAction.LoadPersisted
        : FelixReadinessAction.Rebuild;

    public static bool IsFresh(FelixContext context, DateTimeOffset now, TimeOnly dailyRebuildAt)
    {
        if (context.BuiltAt < LatestRebuildTime(now, dailyRebuildAt)) return false;

        return context.IsCached
            ? context.CacheExpiresAt > now + CacheLead
            : now - context.BuiltAt < InlineTtl;
    }

    /// <summary>The last time, at or before <paramref name="now"/>, that Italian clocks showed <paramref name="dailyRebuildAt"/>.</summary>
    public static DateTimeOffset LatestRebuildTime(DateTimeOffset now, TimeOnly dailyRebuildAt)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Italy).DateTime);
        var candidate = AtItalianTime(today, dailyRebuildAt);
        return candidate <= now ? candidate : AtItalianTime(today.AddDays(-1), dailyRebuildAt);
    }

    private static DateTimeOffset AtItalianTime(DateOnly date, TimeOnly time)
    {
        var wallClock = date.ToDateTime(time);
        return new DateTimeOffset(wallClock, Italy.GetUtcOffset(wallClock));
    }
}
