using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    created_at = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cinemas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "felix_cache",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    cache_name = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_felix_cache", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
                    tmdb_budget = table.Column<int>(type: "integer", nullable: true),
                    tmdb_revenue = table.Column<int>(type: "integer", nullable: true),
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
                    tmdb_enriched_at = table.Column<string>(type: "text", nullable: true),
                    content_rating = table.Column<string>(type: "text", nullable: true),
                    writer = table.Column<string>(type: "text", nullable: true),
                    actors = table.Column<string>(type: "text", nullable: true),
                    released = table.Column<string>(type: "text", nullable: true),
                    imdb_rating = table.Column<string>(type: "text", nullable: true),
                    imdb_votes = table.Column<string>(type: "text", nullable: true),
                    rt_rating = table.Column<string>(type: "text", nullable: true),
                    metacritic_score = table.Column<string>(type: "text", nullable: true),
                    omdb_enriched_at = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tmdb_lookup_cache",
                columns: table => new
                {
                    scraped_title = table.Column<string>(type: "text", nullable: false),
                    scraped_original_title = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    looked_up_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tmdb_lookup_cache", x => new { x.scraped_title, x.scraped_original_title });
                });

            migrationBuilder.CreateTable(
                name: "screenings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    movie_id = table.Column<int>(type: "integer", nullable: false),
                    cinema_id = table.Column<string>(type: "text", nullable: false),
                    datetime = table.Column<string>(type: "text", nullable: false),
                    hall = table.Column<string>(type: "text", nullable: true),
                    is_3d = table.Column<bool>(type: "boolean", nullable: false),
                    is_ov = table.Column<bool>(type: "boolean", nullable: false),
                    subtitle_language = table.Column<string>(type: "text", nullable: true),
                    booking_url = table.Column<string>(type: "text", nullable: true),
                    meta = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenings", x => x.id);
                    table.ForeignKey(
                        name: "fk_screenings_cinemas_cinema_id",
                        column: x => x.cinema_id,
                        principalTable: "cinemas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_screenings_movies_movie_id",
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
                name: "ix_movies_imdb_id",
                table: "movies",
                column: "imdb_id",
                unique: true,
                filter: "imdb_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_movies_slug",
                table: "movies",
                column: "slug",
                unique: true,
                filter: "slug IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_movies_tmdb_id",
                table: "movies",
                column: "tmdb_id",
                unique: true,
                filter: "tmdb_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_screenings_cinema_id_movie_id_datetime",
                table: "screenings",
                columns: new[] { "cinema_id", "movie_id", "datetime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_screenings_movie_id",
                table: "screenings",
                column: "movie_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "felix_cache");

            migrationBuilder.DropTable(
                name: "screenings");

            migrationBuilder.DropTable(
                name: "tmdb_lookup_cache");

            migrationBuilder.DropTable(
                name: "cinemas");

            migrationBuilder.DropTable(
                name: "movies");
        }
    }
}
