namespace Felinoir.UnitTests.TestSupport;

/// <summary>A <see cref="TimeProvider"/> frozen at <see cref="Now"/>, which tests may move.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
