using Npgsql;

namespace Felinoir.Infrastructure.Configuration;

/// <summary>
/// Normalizes the <c>DATABASE_URL</c> environment variable into an Npgsql
/// keyword/value connection string.
/// <para>
/// Accepts URI form (<c>postgres://user:pass@host:5432/db?sslmode=require</c>,
/// as used by the original Node backend and most managed Postgres providers)
/// and passes already-keyword strings (<c>Host=...;Username=...</c>) through unchanged.
/// </para>
/// </summary>
public static class DatabaseUrl
{
    public static string ToNpgsqlConnectionString(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("DATABASE_URL is not set or is empty.", nameof(databaseUrl));

        // Already keyword/value form — hand it straight to Npgsql.
        if (!LooksLikeUri(databaseUrl))
            return databaseUrl;

        var uri = new Uri(databaseUrl);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0 && userInfo[0].Length > 0)
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        if (userInfo.Length > 1)
            builder.Password = Uri.UnescapeDataString(userInfo[1]);

        foreach (var (key, value) in ParseQuery(uri.Query))
            ApplyQueryParam(builder, key, value);

        return builder.ConnectionString;
    }

    private static bool LooksLikeUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
            yield break;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            yield return (key, value);
        }
    }

    private static void ApplyQueryParam(NpgsqlConnectionStringBuilder builder, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "sslmode":
                // libpq spells these "verify-ca"/"verify-full"; Npgsql's enum is "VerifyCA"/"VerifyFull".
                builder.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), ignoreCase: true);
                break;
            default:
                // Pass through anything else Npgsql recognizes (application_name, etc.); ignore the rest.
                try { builder[key] = value; }
                catch (ArgumentException) { /* unknown provider-specific param — drop it */ }
                break;
        }
    }
}
