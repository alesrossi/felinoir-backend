using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Brings the three objects Baseline keeps in production's shape in line with the EF model:
    /// the unique indexes of ai_match_verdicts and tmdb_lookup_cache become their primary keys
    /// (same columns, so the scrapers' ON CONFLICT targets still match), and the
    /// lobby_participants_one_per_device constraint becomes a unique index of the same name
    /// (the API maps 23505 on it by name, which a unique index reports just the same).
    /// </summary>
    public partial class NormaliseKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE ai_match_verdicts
                  ADD CONSTRAINT ai_match_verdicts_pkey PRIMARY KEY USING INDEX idx_ai_match_verdicts_key;

                ALTER TABLE tmdb_lookup_cache
                  ADD CONSTRAINT tmdb_lookup_cache_pkey PRIMARY KEY USING INDEX idx_tmdb_lookup_cache_key;

                ALTER TABLE lobby_participants DROP CONSTRAINT lobby_participants_one_per_device;
                CREATE UNIQUE INDEX lobby_participants_one_per_device
                  ON lobby_participants (lobby_id, device_hash);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE ai_match_verdicts DROP CONSTRAINT ai_match_verdicts_pkey;
                CREATE UNIQUE INDEX idx_ai_match_verdicts_key
                  ON ai_match_verdicts (scraped_title, scraped_original_title);

                ALTER TABLE tmdb_lookup_cache DROP CONSTRAINT tmdb_lookup_cache_pkey;
                CREATE UNIQUE INDEX idx_tmdb_lookup_cache_key
                  ON tmdb_lookup_cache (scraped_title, scraped_original_title);

                DROP INDEX lobby_participants_one_per_device;
                ALTER TABLE lobby_participants
                  ADD CONSTRAINT lobby_participants_one_per_device UNIQUE (lobby_id, device_hash);
                """);
        }
    }
}
