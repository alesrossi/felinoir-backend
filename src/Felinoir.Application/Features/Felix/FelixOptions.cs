namespace Felinoir.Application.Features.Felix;

/// <summary>Felix settings (configuration section <c>Felix</c>).</summary>
public sealed class FelixOptions
{
    public const string Section = "Felix";

    /// <summary>
    /// Italian time after which a context built earlier is stale, so the first question of the day
    /// rebuilds from the night's scrape. After the scrapers' nightly run; 05:00 by default.
    /// </summary>
    public TimeOnly DailyRebuildAt { get; set; } = new(5, 0);
}
