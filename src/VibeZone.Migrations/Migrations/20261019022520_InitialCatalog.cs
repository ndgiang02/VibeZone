using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "genres",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "genres",
                columns: new[] { "id", "name", "slug" },
                values: new object[,]
                {
                    { 1, "Electronic", "electronic" },
                    { 2, "House", "house" },
                    { 3, "Techno", "techno" },
                    { 4, "Drum & Bass", "drum-and-bass" },
                    { 5, "Dubstep", "dubstep" },
                    { 6, "Ambient", "ambient" },
                    { 7, "Lo-fi", "lo-fi" },
                    { 8, "Hip-Hop", "hip-hop" },
                    { 9, "R&B", "rnb" },
                    { 10, "Pop", "pop" },
                    { 11, "V-Pop", "v-pop" },
                    { 12, "K-Pop", "k-pop" },
                    { 13, "Rock", "rock" },
                    { 14, "Indie", "indie" },
                    { 15, "Metal", "metal" },
                    { 16, "Jazz", "jazz" },
                    { 17, "Classical", "classical" },
                    { 18, "Folk", "folk" },
                    { 19, "Country", "country" },
                    { 20, "Latin", "latin" },
                    { 21, "Reggae", "reggae" },
                    { 22, "Soundtrack", "soundtrack" },
                    { 23, "Podcast & Spoken Word", "podcast" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_genres_slug",
                schema: "catalog",
                table: "genres",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genres",
                schema: "catalog");
        }
    }
}
