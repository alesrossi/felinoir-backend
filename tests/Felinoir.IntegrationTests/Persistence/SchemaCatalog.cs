using Npgsql;

namespace Felinoir.IntegrationTests.Persistence;

/// <summary>
/// Describes the <c>public</c> schema of a database as sorted, one-line facts read from the
/// Postgres catalog: extensions, columns, constraints, indexes, triggers and functions. Two
/// databases with the same facts have the same schema, whatever order their columns were
/// added in (the scrapers added many with <c>ALTER TABLE</c>, EF creates them in one go).
/// EF's own <c>__EFMigrationsHistory</c> is left out.
/// </summary>
internal static class SchemaCatalog
{
    private const string History = "__EFMigrationsHistory";

    private static readonly string[] Queries =
    [
        """
        SELECT 'extension ' || extname
        FROM pg_extension WHERE extname <> 'plpgsql'
        """,
        $"""
        SELECT format('column %s.%s %s%s%s%s',
                 c.relname, a.attname, format_type(a.atttypid, a.atttypmod),
                 CASE WHEN a.attnotnull THEN ' not null' ELSE '' END,
                 CASE a.attidentity WHEN 'a' THEN ' identity always' WHEN 'd' THEN ' identity by default' ELSE '' END,
                 COALESCE(' default ' || pg_get_expr(d.adbin, d.adrelid), ''))
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid AND c.relkind = 'r'
        JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
        LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
        WHERE a.attnum > 0 AND NOT a.attisdropped AND c.relname <> '{History}'
        """,
        $"""
        SELECT format('constraint %s.%s %s', c.relname, con.conname, pg_get_constraintdef(con.oid))
        FROM pg_constraint con
        JOIN pg_class c ON c.oid = con.conrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
        WHERE c.relname <> '{History}'
        """,
        $"""
        SELECT format('index %s %s', indexname, indexdef)
        FROM pg_indexes WHERE schemaname = 'public' AND tablename <> '{History}'
        """,
        """
        SELECT 'trigger ' || pg_get_triggerdef(t.oid)
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
        WHERE NOT t.tgisinternal
        """,
        // Functions created by an extension (pg_trgm lives in public) are the extension's, not ours.
        """
        SELECT 'function ' || regexp_replace(pg_get_functiondef(p.oid), '\s+', ' ', 'g')
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace AND n.nspname = 'public'
        WHERE NOT EXISTS (
            SELECT 1 FROM pg_depend dep
            WHERE dep.objid = p.oid AND dep.classid = 'pg_proc'::regclass AND dep.deptype = 'e')
        """,
    ];

    public static async Task<IReadOnlyList<string>> CaptureAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        var facts = new List<string>();
        foreach (var sql in Queries)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) facts.Add(reader.GetString(0));
        }

        facts.Sort(StringComparer.Ordinal);
        return facts;
    }

    /// <summary>Lines only in <paramref name="expected"/> (-) and only in <paramref name="actual"/> (+).</summary>
    public static string Diff(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var missing = expected.Except(actual).Select(f => "- " + f);
        var extra = actual.Except(expected).Select(f => "+ " + f);
        return string.Join('\n', missing.Concat(extra));
    }
}
