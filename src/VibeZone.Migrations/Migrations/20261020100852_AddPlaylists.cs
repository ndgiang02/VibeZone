using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "playlists");

            migrationBuilder.CreateTable(
                name: "playlists",
                schema: "playlists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    track_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlists", x => x.id);
                    table.CheckConstraint("ck_playlists_track_count", "track_count >= 0");
                    table.CheckConstraint("ck_playlists_visibility", "visibility IN ('public', 'private')");
                });

            migrationBuilder.CreateTable(
                name: "playlist_tracks",
                schema: "playlists",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_tracks", x => new { x.playlist_id, x.track_id });
                    table.CheckConstraint("ck_playlist_tracks_position", "position > 0");
                    table.ForeignKey(
                        name: "fk_playlist_tracks_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalSchema: "playlists",
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_tracks_playlist_id_position",
                schema: "playlists",
                table: "playlist_tracks",
                columns: new[] { "playlist_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_tracks_track_id",
                schema: "playlists",
                table: "playlist_tracks",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_owner_user_id_updated_at",
                schema: "playlists",
                table: "playlists",
                columns: new[] { "owner_user_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_playlists_public",
                schema: "playlists",
                table: "playlists",
                column: "updated_at",
                descending: new bool[0],
                filter: "visibility = 'public' AND track_count > 0");

            // Cross-module foreign keys (ADR-0001), added by hand: users and tracks belong to other models.
            migrationBuilder.AddForeignKey(
                name: "fk_playlists_users_owner_user_id", schema: "playlists", table: "playlists", column: "owner_user_id",
                principalSchema: "identity", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "fk_playlist_tracks_tracks_track_id", schema: "playlists", table: "playlist_tracks", column: "track_id",
                principalSchema: "catalog", principalTable: "tracks", principalColumn: "id", onDelete: ReferentialAction.Restrict);

            // Search (EfPlaylistRepository.PagePublicAsync); public.vz_fold comes from AddAlbumsAndSearch.
            migrationBuilder.Sql("""
                ALTER TABLE playlists.playlists ADD COLUMN search_vector tsvector GENERATED ALWAYS AS
                    (to_tsvector('simple', public.vz_fold(title || ' ' || coalesce(description, '')))) STORED;
                CREATE INDEX ix_playlists_search ON playlists.playlists USING gin (search_vector);
                CREATE INDEX ix_playlists_title_trgm ON playlists.playlists USING gin (public.vz_fold(title) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_tracks",
                schema: "playlists");

            migrationBuilder.DropTable(
                name: "playlists",
                schema: "playlists");
        }
    }
}
