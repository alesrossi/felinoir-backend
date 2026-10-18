using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovieCreditsAndAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scraper adds these columns itself on connect (IF NOT EXISTS), so on the
            // production database they usually exist already. Kept idempotent so this
            // migration applies cleanly either way.
            migrationBuilder.Sql("ALTER TABLE movies ADD COLUMN IF NOT EXISTS tmdb_credits TEXT;");
            migrationBuilder.Sql("ALTER TABLE movies ADD COLUMN IF NOT EXISTS awards TEXT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "awards",
                table: "movies");

            migrationBuilder.DropColumn(
                name: "tmdb_credits",
                table: "movies");
        }
    }
}
