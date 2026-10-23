using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_id",
                schema: "catalog",
                table: "artists",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "banner_id",
                schema: "catalog",
                table: "artists",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "artist_images",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artist_images", x => x.id);
                    table.CheckConstraint("ck_artist_images_kind", "kind IN ('avatarsmall', 'avatarlarge', 'bannersmall', 'bannerlarge')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_artist_images_artist_id_kind",
                schema: "media",
                table: "artist_images",
                columns: new[] { "artist_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artist_images_object_key",
                schema: "media",
                table: "artist_images",
                column: "object_key",
                unique: true);

            // Cross-module foreign key (ADR-0001), added by hand: Artist belongs to the Catalog model.
            migrationBuilder.AddForeignKey(
                name: "fk_artist_images_artists_artist_id", schema: "media", table: "artist_images", column: "artist_id",
                principalSchema: "catalog", principalTable: "artists", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artist_images",
                schema: "media");

            migrationBuilder.DropColumn(
                name: "avatar_id",
                schema: "catalog",
                table: "artists");

            migrationBuilder.DropColumn(
                name: "banner_id",
                schema: "catalog",
                table: "artists");
        }
    }
}
