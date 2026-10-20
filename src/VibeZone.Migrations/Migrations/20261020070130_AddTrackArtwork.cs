using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackArtwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_kind",
                schema: "media",
                table: "media_assets");

            migrationBuilder.AddColumn<Guid>(
                name: "artwork_id",
                schema: "catalog",
                table: "tracks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_kind",
                schema: "media",
                table: "media_assets",
                sql: "kind IN ('original', 'playbackaac', 'waveformpeaks', 'artworksmall', 'artworklarge')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_kind",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "artwork_id",
                schema: "catalog",
                table: "tracks");

            // Rows only; the stored cover objects stay orphaned in object storage after a rollback.
            migrationBuilder.Sql("DELETE FROM media.media_assets WHERE kind IN ('artworksmall', 'artworklarge');");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_kind",
                schema: "media",
                table: "media_assets",
                sql: "kind IN ('original', 'playbackaac', 'waveformpeaks')");
        }
    }
}
