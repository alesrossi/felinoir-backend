using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Felinoir.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Cities become their own table (rework plan, step 2). Rome is the only city: every
    /// existing cinema is listed under it, including those outside the municipality, whose
    /// town stays in their address. Cinemas gain the coordinates and open-air flag that used to
    /// live only in the backend's copy of cinemas.yml (values copied from it here; the scrapers'
    /// cinemas.yml keeps them up to date from now on). felix_cache is keyed by city.
    /// </summary>
    public partial class Cities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')"),
                    updated_at = table.Column<string>(type: "text", nullable: false, defaultValueSql: "to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cities_pkey", x => x.id);
                });

            migrationBuilder.Sql("INSERT INTO cities (id, name) VALUES ('rome', 'Roma');");

            // cinemas.city (free text, the town) -> cinemas.city_id (the city it is listed under).
            migrationBuilder.AddColumn<string>(
                name: "city_id",
                table: "cinemas",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE cinemas SET city_id = 'rome';
                ALTER TABLE cinemas ALTER COLUMN city_id SET NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "idx_cinemas_city",
                table: "cinemas");

            migrationBuilder.DropColumn(
                name: "city",
                table: "cinemas");

            migrationBuilder.CreateIndex(
                name: "idx_cinemas_city_id",
                table: "cinemas",
                column: "city_id");

            migrationBuilder.AddForeignKey(
                name: "cinemas_city_id_fkey",
                table: "cinemas",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Coordinates and open-air flag, from cinemas.yml.
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "cinemas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "cinemas",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "open_air",
                table: "cinemas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE cinemas c
                   SET latitude = v.latitude, longitude = v.longitude, open_air = v.open_air
                  FROM (VALUES
                  ('cinema-adriano', 41.905869, 12.46958, false),
                  ('cinema-andromeda', 41.913275, 12.418303, false),
                  ('cinema-antares', 41.938427, 12.535064, false),
                  ('cinema-cineland', 41.746259, 12.28691, false),
                  ('casa-del-cinema', 41.91088, 12.487702, false),
                  ('cinema-aquila', 41.89058, 12.5253, false),
                  ('cinema-san-timoteo', 41.754262, 12.353797, false),
                  ('cinema-caravaggio', 41.919784, 12.492586, false),
                  ('cinema-broadway', 41.882231, 12.565766, false),
                  ('cinema-doria', 41.909573, 12.450729, false),
                  ('cinema-eurcine', 41.8338771, 12.4684719, false),
                  ('cinema-farnese', 41.8955774, 12.4721586, false),
                  ('cinema-giulio-cesare', 41.9108397, 12.4638869, false),
                  ('cinema-intrastevere', 41.8921527, 12.468573, false),
                  ('cinema-nuovo-olimpia', 41.9027157, 12.478536, false),
                  ('cinema-quattro-fontane', 41.9013, 12.4919985, false),
                  ('cinema-savoy', 41.9113102, 12.4995899, false),
                  ('thescreen-ottavia', 41.958702, 12.399677, false),
                  ('cinema-tibur', 41.8973224, 12.515008, false),
                  ('cinema-trianon', 41.8750095, 12.5260561, false),
                  ('multiplex-cinema-augustus', 41.689402, 12.7887, false),
                  ('multisala-ariston', 41.724403, 13.012143, false),
                  ('multisala-alhambra', 41.9037, 12.4296, false),
                  ('multisala-atlantic', 41.8607808, 12.555606, false),
                  ('multisala-barberini', 41.9036042, 12.489128, false),
                  ('cinema-mignon', 41.911703, 12.498366, false),
                  ('multisala-greenwich', 41.8788587, 12.4741095, false),
                  ('multisala-jolly', 41.9087038, 12.5228122, false),
                  ('cinema-madison', 41.853415, 12.479304, false),
                  ('multisala-lux', 41.9264215, 12.5147436, false),
                  ('multisala-odeon', 41.9495282, 12.4656229, false),
                  ('the-space-moderno', 41.9026992, 12.4964179, false),
                  ('the-space-parco-de-medici', 41.81514328018534, 12.411329220982054, false),
                  ('uci-luxe-maximo', 41.8075878, 12.4859393, false),
                  ('uci-parco-leonardo', 41.7979793, 12.2955904, false),
                  ('uci-porta-di-roma', 41.971398911638744, 12.541209356740922, false),
                  ('uci-roma-est', 41.9147117, 12.6612557, false),
                  ('cinema-troisi', 41.884946, 12.471638, false),
                  ('cinema-madison-alfellini', 41.78689, 12.66888, false),
                  ('cinema-politeama', 41.807999, 12.677493, false),
                  ('the-space-guidonia', 41.957782, 12.708933, false),
                  ('cinema-azzurro-scipioni', 41.90849, 12.45824, false),
                  ('cinema-mancini', 42.052141, 12.61807, false),
                  ('cinema-palazzo-san-lorenzo', 41.898715, 12.516649, false),
                  ('cinema-astoria', 41.4482734, 12.6282633, false),
                  ('cinema-astoria-nettuno', 41.4662573, 12.6819565, false),
                  ('cinema-royal', 42.0932778, 11.7940467, false),
                  ('cinema-giuseppetti', 41.961292228266835, 12.800107565649402, false),
                  ('multisala-cynthianum', 41.71126419974851, 12.689878683149269, false),
                  ('cinema-nuovo-sacher', 41.88453, 12.472374, false),
                  ('san-cosimato', 41.887146, 12.469766, true),
                  ('cervelletta', 41.912735, 12.580386, true),
                  ('monte-ciocci', 41.900547, 12.442249, true)
                  ) AS v (id, latitude, longitude, open_air)
                 WHERE c.id = v.id;
                """);

            // felix_cache: the single row (id = 1) becomes Rome's.
            migrationBuilder.AddColumn<string>(
                name: "city_id",
                table: "felix_cache",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                DELETE FROM felix_cache WHERE id <> 1;
                UPDATE felix_cache SET city_id = 'rome';
                ALTER TABLE felix_cache ALTER COLUMN city_id SET NOT NULL;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "felix_cache_pkey",
                table: "felix_cache");

            migrationBuilder.DropColumn(
                name: "id",
                table: "felix_cache");

            migrationBuilder.AddPrimaryKey(
                name: "felix_cache_pkey",
                table: "felix_cache",
                column: "city_id");

            migrationBuilder.AddForeignKey(
                name: "felix_cache_city_id_fkey",
                table: "felix_cache",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // felix_cache back to the single row id = 1 (Rome's cache, if any, survives).
            migrationBuilder.DropForeignKey(
                name: "felix_cache_city_id_fkey",
                table: "felix_cache");

            migrationBuilder.Sql("DELETE FROM felix_cache WHERE city_id <> 'rome';");

            migrationBuilder.DropPrimaryKey(
                name: "felix_cache_pkey",
                table: "felix_cache");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "felix_cache",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "felix_cache");

            migrationBuilder.AddPrimaryKey(
                name: "felix_cache_pkey",
                table: "felix_cache",
                column: "id");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "cinemas");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "cinemas");

            migrationBuilder.DropColumn(
                name: "open_air",
                table: "cinemas");

            // cinemas.city comes back as the city's name: the town of a cinema listed under a
            // city it isn't in (e.g. Velletri under Rome) is not recoverable.
            migrationBuilder.DropForeignKey(
                name: "cinemas_city_id_fkey",
                table: "cinemas");

            migrationBuilder.DropIndex(
                name: "idx_cinemas_city_id",
                table: "cinemas");

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "cinemas",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE cinemas c SET city = ci.name FROM cities ci WHERE ci.id = c.city_id;
                ALTER TABLE cinemas ALTER COLUMN city SET NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "cinemas");

            migrationBuilder.CreateIndex(
                name: "idx_cinemas_city",
                table: "cinemas",
                column: "city");

            migrationBuilder.DropTable(
                name: "cities");
        }
    }
}
