using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Lobbies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The schema is owned by the scrapers project, so on the production database
            // these objects may exist already. The DDL is the scrapers' own, kept
            // idempotent so this migration applies cleanly either way.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS lobbies (
                  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                  version         bigint NOT NULL DEFAULT 1,
                  screening_id    integer,
                  movie_id        integer,
                  movie_slug      text,
                  movie_title     text NOT NULL,
                  movie_title_en  text,
                  poster_url      text,
                  cinema_id       text NOT NULL,
                  cinema_name     text NOT NULL,
                  starts_at       timestamptz NOT NULL,
                  is_ov           boolean NOT NULL DEFAULT false,
                  is_3d           boolean NOT NULL DEFAULT false,
                  booking_url     text,
                  note            text NOT NULL DEFAULT '',
                  created_at      timestamptz NOT NULL DEFAULT now(),
                  CONSTRAINT lobbies_note_length CHECK (char_length(note) <= 500)
                );
                """);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS lobbies_starts_at_idx ON lobbies (starts_at);");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS lobby_participants (
                  id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                  lobby_id    uuid NOT NULL REFERENCES lobbies (id) ON DELETE CASCADE,
                  name        text NOT NULL,
                  device_hash bytea,
                  joined_at   timestamptz NOT NULL DEFAULT now(),
                  CONSTRAINT lobby_participants_name_length
                    CHECK (char_length(name) BETWEEN 1 AND 40),
                  CONSTRAINT lobby_participants_one_per_device
                    UNIQUE (lobby_id, device_hash)
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS lobby_participants_name_uniq
                  ON lobby_participants (lobby_id, lower(name));
                """);

            // Version counter for polling: every participant change or note change bumps it.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION lobby_bump_version_on_participant_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                  UPDATE lobbies
                     SET version = version + 1
                   WHERE id = coalesce(new.lobby_id, old.lobby_id);
                  RETURN NULL;
                END $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS lobby_participants_bump_version ON lobby_participants;
                CREATE TRIGGER lobby_participants_bump_version
                AFTER INSERT OR UPDATE OR DELETE ON lobby_participants
                FOR EACH ROW EXECUTE FUNCTION lobby_bump_version_on_participant_change();
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION lobby_bump_version_on_note_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                  new.version := old.version + 1;
                  RETURN new;
                END $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS lobbies_bump_version_on_note ON lobbies;
                CREATE TRIGGER lobbies_bump_version_on_note
                BEFORE UPDATE OF note ON lobbies
                FOR EACH ROW
                WHEN (old.note IS DISTINCT FROM new.note)
                EXECUTE FUNCTION lobby_bump_version_on_note_change();
                """);

            // Cleanup of lobbies whose screening started more than 48 hours ago.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION delete_expired_lobbies() RETURNS integer
                LANGUAGE sql AS $$
                  WITH deleted AS (
                    DELETE FROM lobbies
                     WHERE starts_at < now() - interval '48 hours'
                    RETURNING 1
                  )
                  SELECT count(*)::int FROM deleted;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS delete_expired_lobbies();");

            // Dropping the tables drops their triggers and indexes with them.
            migrationBuilder.DropTable(
                name: "lobby_participants");

            migrationBuilder.DropTable(
                name: "lobbies");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS lobby_bump_version_on_participant_change();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS lobby_bump_version_on_note_change();");
        }
    }
}
