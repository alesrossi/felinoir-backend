using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiMatchVerdicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scraper creates these objects itself on connect (IF NOT EXISTS), so on the
            // production database they usually exist already. The DDL is the scraper's own,
            // kept idempotent so this migration applies cleanly either way.
            migrationBuilder.Sql(
                "ALTER TABLE tmdb_lookup_cache ADD COLUMN IF NOT EXISTS scraped_poster_url TEXT;");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS ai_match_verdicts (
                  scraped_title          TEXT NOT NULL,
                  scraped_original_title TEXT NOT NULL DEFAULT '',
                  verdict                TEXT NOT NULL CHECK (verdict IN ('match', 'not_a_film', 'unknown')),
                  tmdb_id                INTEGER,
                  confidence             TEXT NOT NULL CHECK (confidence IN ('high', 'low')),
                  checked_tmdb_id        INTEGER,
                  rejected_tmdb_ids      INTEGER[] NOT NULL DEFAULT '{}',
                  reason                 TEXT,
                  model                  TEXT,
                  verified_at            TEXT NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_match_verdicts_key
                  ON ai_match_verdicts (scraped_title, scraped_original_title);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_match_verdicts");

            migrationBuilder.DropColumn(
                name: "scraped_poster_url",
                table: "tmdb_lookup_cache");
        }
    }
}
