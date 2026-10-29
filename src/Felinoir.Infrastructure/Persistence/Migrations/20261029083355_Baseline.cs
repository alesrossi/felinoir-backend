using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The database schema exactly as the scrapers had built it in production (see
    /// tests/schema/production.sql), squashed into one migration when this repository took
    /// ownership of the schema (ADR 0001). Existing databases already have all of it and are
    /// marked as having applied this migration instead of running it (see DatabaseMigrator).
    ///
    /// Three things are kept in production's shape here although the EF model describes them
    /// differently: ai_match_verdicts and tmdb_lookup_cache have a unique index but no primary
    /// key, and lobby_participants_one_per_device is a unique constraint. NormaliseKeys, the
    /// next migration, brings them in line with the model. Expression indexes, functions and
    /// triggers EF can't model are raw SQL, copied verbatim from production.
    /// </summary>
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "ai_match_verdicts",
                columns: table => new
                {
                    scraped_title = table.Column<string>(type: "text", nullable: false),
                    scraped_original_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    verdict = table.Column<string>(type: "text", nullable: false),
                    tmdb_id = table.Column<int>(type: "integer", nullable: true),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    checked_tmdb_id = table.Column<int>(type: "integer", nullable: true),
                    rejected_tmdb_ids = table.Column<int[]>(type: "integer[]", nullable: false, defaultValueSql: "'{}'"),
                    reason = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "text", nullable: true),
                    verified_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.CheckConstraint("ai_match_verdicts_confidence_check", "confidence IN ('high', 'low')");
                    table.CheckConstraint("ai_match_verdicts_verdict_check", "verdict IN ('match', 'not_a_film', 'unknown')");
                });

            migrationBuilder.CreateTable(
                name: "cinemas",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    chain = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: false),
                    country = table.Column<string>(type: "text", nullable: false, defaultValue: "IT"),
                    website = table.Column<string>(type: "text", nullable: false),
                    schedule_url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    updated_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cinemas_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "felix_cache",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    cache_name = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("felix_cache_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lobbies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    screening_id = table.Column<int>(type: "integer", nullable: true),
                    movie_id = table.Column<int>(type: "integer", nullable: true),
                    movie_slug = table.Column<string>(type: "text", nullable: true),
                    movie_title = table.Column<string>(type: "text", nullable: false),
                    movie_title_en = table.Column<string>(type: "text", nullable: true),
                    poster_url = table.Column<string>(type: "text", nullable: true),
                    cinema_id = table.Column<string>(type: "text", nullable: false),
                    cinema_name = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_ov = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_3d = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    booking_url = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("lobbies_pkey", x => x.id);
                    table.CheckConstraint("lobbies_note_length", "char_length(note) <= 500");
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    original_title = table.Column<string>(type: "text", nullable: true),
                    director = table.Column<string>(type: "text", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    genres = table.Column<string>(type: "text", nullable: true),
                    synopsis = table.Column<string>(type: "text", nullable: true),
                    poster_url = table.Column<string>(type: "text", nullable: true),
                    trailer_url = table.Column<string>(type: "text", nullable: true),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    rating = table.Column<string>(type: "text", nullable: true),
                    slug = table.Column<string>(type: "text", nullable: true),
                    tmdb_id = table.Column<int>(type: "integer", nullable: true),
                    imdb_id = table.Column<string>(type: "text", nullable: true),
                    tmdb_rating = table.Column<string>(type: "text", nullable: true),
                    tmdb_votes = table.Column<string>(type: "text", nullable: true),
                    tmdb_title = table.Column<string>(type: "text", nullable: true),
                    tmdb_title_en = table.Column<string>(type: "text", nullable: true),
                    tmdb_original_title = table.Column<string>(type: "text", nullable: true),
                    tmdb_plot = table.Column<string>(type: "text", nullable: true),
                    tmdb_plot_en = table.Column<string>(type: "text", nullable: true),
                    tmdb_genre = table.Column<string>(type: "text", nullable: true),
                    tmdb_genre_en = table.Column<string>(type: "text", nullable: true),
                    tmdb_language = table.Column<string>(type: "text", nullable: true),
                    tmdb_country = table.Column<string>(type: "text", nullable: true),
                    tmdb_poster_url = table.Column<string>(type: "text", nullable: true),
                    tmdb_trailer_url_it = table.Column<string>(type: "text", nullable: true),
                    tmdb_trailer_url_en = table.Column<string>(type: "text", nullable: true),
                    tmdb_popularity = table.Column<string>(type: "text", nullable: true),
                    tmdb_status = table.Column<string>(type: "text", nullable: true),
                    tmdb_tagline = table.Column<string>(type: "text", nullable: true),
                    tmdb_budget = table.Column<long>(type: "bigint", nullable: true),
                    tmdb_revenue = table.Column<long>(type: "bigint", nullable: true),
                    tmdb_homepage = table.Column<string>(type: "text", nullable: true),
                    tmdb_collection = table.Column<string>(type: "text", nullable: true),
                    tmdb_production_companies = table.Column<string>(type: "text", nullable: true),
                    tmdb_production_countries = table.Column<string>(type: "text", nullable: true),
                    tmdb_spoken_languages = table.Column<string>(type: "text", nullable: true),
                    tmdb_backdrops = table.Column<string>(type: "text", nullable: true),
                    tmdb_videos = table.Column<string>(type: "text", nullable: true),
                    tmdb_keywords = table.Column<string>(type: "text", nullable: true),
                    tmdb_recommendations = table.Column<string>(type: "text", nullable: true),
                    tmdb_watch_providers = table.Column<string>(type: "text", nullable: true),
                    tmdb_credits = table.Column<string>(type: "text", nullable: true),
                    tmdb_enriched_at = table.Column<string>(type: "text", nullable: true),
                    content_rating = table.Column<string>(type: "text", nullable: true),
                    writer = table.Column<string>(type: "text", nullable: true),
                    actors = table.Column<string>(type: "text", nullable: true),
                    released = table.Column<string>(type: "text", nullable: true),
                    imdb_rating = table.Column<string>(type: "text", nullable: true),
                    imdb_votes = table.Column<string>(type: "text", nullable: true),
                    rt_rating = table.Column<string>(type: "text", nullable: true),
                    metacritic_score = table.Column<string>(type: "text", nullable: true),
                    awards = table.Column<string>(type: "text", nullable: true),
                    omdb_enriched_at = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    updated_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    canonical_title = table.Column<string>(type: "text", nullable: true),
                    has_matches = table.Column<string>(type: "text", nullable: false, defaultValue: "FailedMatch"),
                    tmdb_last_attempt_at = table.Column<string>(type: "text", nullable: true),
                    tmdb_match_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("movies_pkey", x => x.id);
                    table.CheckConstraint("movies_has_matches_check", "has_matches IN ('FailedMatch', 'NoMatch', 'FoundMatch')");
                });

            migrationBuilder.CreateTable(
                name: "tmdb_lookup_cache",
                columns: table => new
                {
                    scraped_title = table.Column<string>(type: "text", nullable: false),
                    scraped_original_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    looked_up_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    scraped_poster_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "lobby_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    lobby_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    device_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("lobby_participants_pkey", x => x.id);
                    table.CheckConstraint("lobby_participants_name_length", "char_length(name) between 1 and 40");
                    table.ForeignKey(
                        name: "lobby_participants_lobby_id_fkey",
                        column: x => x.lobby_id,
                        principalTable: "lobbies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "screenings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    movie_id = table.Column<int>(type: "integer", nullable: false),
                    cinema_id = table.Column<string>(type: "text", nullable: false),
                    datetime = table.Column<string>(type: "text", nullable: false),
                    hall = table.Column<string>(type: "text", nullable: true),
                    is_3d = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_ov = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subtitle_language = table.Column<string>(type: "text", nullable: true),
                    booking_url = table.Column<string>(type: "text", nullable: true),
                    meta = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    updated_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("screenings_pkey", x => x.id);
                    table.UniqueConstraint("screenings_cinema_id_movie_id_datetime_key", x => new { x.cinema_id, x.movie_id, x.datetime });
                    table.ForeignKey(
                        name: "screenings_cinema_id_fkey",
                        column: x => x.cinema_id,
                        principalTable: "cinemas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "screenings_movie_id_fkey",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_cinemas_city",
                table: "cinemas",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "lobbies_starts_at_idx",
                table: "lobbies",
                column: "starts_at");

            migrationBuilder.Sql("""
                ALTER TABLE lobby_participants
                  ADD CONSTRAINT lobby_participants_one_per_device UNIQUE (lobby_id, device_hash);
                """);

            migrationBuilder.CreateIndex(
                name: "idx_movies_canonical_trgm",
                table: "movies",
                column: "canonical_title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "idx_movies_external_id",
                table: "movies",
                column: "external_id");

            migrationBuilder.CreateIndex(
                name: "idx_movies_imdb_id",
                table: "movies",
                column: "imdb_id",
                unique: true,
                filter: "imdb_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_movies_slug",
                table: "movies",
                column: "slug",
                unique: true,
                filter: "slug IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_movies_title",
                table: "movies",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "idx_movies_tmdb_id",
                table: "movies",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_screenings_cinema",
                table: "screenings",
                column: "cinema_id");

            migrationBuilder.CreateIndex(
                name: "idx_screenings_datetime",
                table: "screenings",
                column: "datetime");

            migrationBuilder.CreateIndex(
                name: "idx_screenings_movie",
                table: "screenings",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "idx_ai_match_verdicts_key",
                table: "ai_match_verdicts",
                columns: new[] { "scraped_title", "scraped_original_title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tmdb_lookup_cache_key",
                table: "tmdb_lookup_cache",
                columns: new[] { "scraped_title", "scraped_original_title" },
                unique: true);

            // The scrapers' dedup key.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX idx_movies_unique ON movies (title, COALESCE(original_title, ''));");

            // Case-insensitive participant names; 23505 on it means "name taken".
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX lobby_participants_name_uniq ON lobby_participants (lobby_id, lower(name));");

            migrationBuilder.Sql("""
                CREATE FUNCTION delete_expired_lobbies() RETURNS integer
                    LANGUAGE sql
                    AS $$
                    WITH deleted AS (
                      DELETE FROM lobbies
                       WHERE starts_at < NOW() - INTERVAL '48 hours'
                      RETURNING 1
                    )
                    SELECT count(*)::int FROM deleted;
                  $$;
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION lobby_bump_version_on_note_change() RETURNS trigger
                    LANGUAGE plpgsql
                    AS $$
                  BEGIN
                    NEW.version := OLD.version + 1;
                    RETURN NEW;
                  END $$;

                CREATE TRIGGER lobbies_bump_version_on_note
                  BEFORE UPDATE OF note ON lobbies
                  FOR EACH ROW WHEN (OLD.note IS DISTINCT FROM NEW.note)
                  EXECUTE FUNCTION lobby_bump_version_on_note_change();
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION lobby_bump_version_on_participant_change() RETURNS trigger
                    LANGUAGE plpgsql
                    AS $$
                  BEGIN
                    UPDATE lobbies
                       SET version = version + 1
                     WHERE id = COALESCE(NEW.lobby_id, OLD.lobby_id);
                    RETURN NULL;
                  END $$;

                CREATE TRIGGER lobby_participants_bump_version
                  AFTER INSERT OR UPDATE OR DELETE ON lobby_participants
                  FOR EACH ROW EXECUTE FUNCTION lobby_bump_version_on_participant_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Triggers go with their tables; the functions don't.
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS delete_expired_lobbies();
                DROP FUNCTION IF EXISTS lobby_bump_version_on_note_change() CASCADE;
                DROP FUNCTION IF EXISTS lobby_bump_version_on_participant_change() CASCADE;
                """);

            migrationBuilder.DropTable(
                name: "ai_match_verdicts");

            migrationBuilder.DropTable(
                name: "felix_cache");

            migrationBuilder.DropTable(
                name: "lobby_participants");

            migrationBuilder.DropTable(
                name: "screenings");

            migrationBuilder.DropTable(
                name: "tmdb_lookup_cache");

            migrationBuilder.DropTable(
                name: "lobbies");

            migrationBuilder.DropTable(
                name: "cinemas");

            migrationBuilder.DropTable(
                name: "movies");

            // Npgsql doesn't drop extensions when reverting the annotation.
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm;");
        }
    }
}
