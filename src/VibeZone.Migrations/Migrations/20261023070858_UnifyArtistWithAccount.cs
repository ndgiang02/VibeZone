using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class UnifyArtistWithAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0013: the artist profile shows the account's handle, display name and picture. Slugs go through
            // the (unique, 36-char) artist id first so two existing slugs can't collide halfway through the update.
            migrationBuilder.Sql("UPDATE catalog.artists SET slug = id::text;");
            migrationBuilder.Sql("""
                UPDATE catalog.artists a
                SET slug = u.user_name, name = p.display_name, avatar_id = p.avatar_id, updated_at = now()
                FROM identity.users u
                JOIN identity.user_profiles p ON p.user_id = u.id
                WHERE u.id = a.user_id;
                """);

            // Artist avatars are gone (the account picture replaces them). Their stored objects are left as
            // orphans in object storage; nothing references them any more.
            migrationBuilder.Sql("DELETE FROM media.artist_images WHERE kind IN ('avatarsmall', 'avatarlarge');");

            migrationBuilder.DropCheckConstraint(
                name: "ck_artist_images_kind",
                schema: "media",
                table: "artist_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_artist_images_kind",
                schema: "media",
                table: "artist_images",
                sql: "kind IN ('bannersmall', 'bannerlarge')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the previous artist names, slugs and avatars are not kept.
            migrationBuilder.DropCheckConstraint(
                name: "ck_artist_images_kind",
                schema: "media",
                table: "artist_images");

            migrationBuilder.AddCheckConstraint(
                name: "ck_artist_images_kind",
                schema: "media",
                table: "artist_images",
                sql: "kind IN ('avatarsmall', 'avatarlarge', 'bannersmall', 'bannerlarge')");
        }
    }
}
