namespace Felinoir.Infrastructure.Persistence.Configurations;

/// <summary>Column default expressions shared by several tables.</summary>
internal static class SqlDefaults
{
    /// <summary>The current time as an ISO 8601 UTC TEXT timestamp, e.g. <c>2026-10-29T08:00:00Z</c>.</summary>
    public const string UtcNowText = """to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')""";
}
