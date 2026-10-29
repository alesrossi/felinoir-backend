using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Lobby aggregate owns its version (rework plan, step 5): it increments it on every change and
    /// EF uses it as the concurrency token, so the triggers that bumped it in the database are dropped
    /// with their functions. delete_expired_lobbies() stays.
    ///
    /// lobby_participants_one_per_device becomes a DEFERRABLE INITIALLY DEFERRED unique constraint,
    /// checked at commit: claiming an entry moves a device between two rows in one save, and the
    /// order of the two UPDATEs isn't ours to choose. EF can't model a deferrable constraint, so it
    /// lives here only; the model has a plain index on lobby_id for the lookup instead.
    /// </summary>
    public partial class LobbyVersionInDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER lobbies_bump_version_on_note ON lobbies;
                DROP TRIGGER lobby_participants_bump_version ON lobby_participants;
                DROP FUNCTION lobby_bump_version_on_note_change();
                DROP FUNCTION lobby_bump_version_on_participant_change();
                """);

            migrationBuilder.DropIndex(
                name: "lobby_participants_one_per_device",
                table: "lobby_participants");

            migrationBuilder.Sql("""
                ALTER TABLE lobby_participants
                  ADD CONSTRAINT lobby_participants_one_per_device UNIQUE (lobby_id, device_hash)
                  DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.CreateIndex(
                name: "lobby_participants_lobby_id_idx",
                table: "lobby_participants",
                column: "lobby_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "lobby_participants_lobby_id_idx",
                table: "lobby_participants");

            migrationBuilder.Sql("ALTER TABLE lobby_participants DROP CONSTRAINT lobby_participants_one_per_device;");

            migrationBuilder.CreateIndex(
                name: "lobby_participants_one_per_device",
                table: "lobby_participants",
                columns: new[] { "lobby_id", "device_hash" },
                unique: true);

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
    }
}
