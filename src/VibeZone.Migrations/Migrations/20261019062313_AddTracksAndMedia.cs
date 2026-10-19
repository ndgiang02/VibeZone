using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTracksAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "media");

            migrationBuilder.CreateTable(
                name: "artists",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    codec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    bitrate = table.Column<int>(type: "integer", nullable: true),
                    sample_rate = table.Column<int>(type: "integer", nullable: true),
                    channels = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_kind", "kind IN ('original', 'playbackaac', 'waveformpeaks')");
                });

            migrationBuilder.CreateTable(
                name: "upload_jobs",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    declared_content_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upload_jobs", x => x.id);
                    table.CheckConstraint("ck_upload_jobs_size", "size_bytes > 0");
                    table.CheckConstraint("ck_upload_jobs_status", "status IN ('uploaded', 'processing', 'completed', 'failed', 'cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    genre_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    processing_error = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracks", x => x.id);
                    table.CheckConstraint("ck_tracks_duration", "duration_ms IS NULL OR duration_ms > 0");
                    table.CheckConstraint("ck_tracks_status", "status IN ('draft', 'processing', 'ready', 'failed', 'cancelled')");
                    table.CheckConstraint("ck_tracks_visibility", "visibility IN ('public', 'unlisted', 'private')");
                    table.ForeignKey(
                        name: "fk_tracks_artists_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracks_genres_genre_id",
                        column: x => x.genre_id,
                        principalSchema: "catalog",
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artists_slug",
                schema: "catalog",
                table: "artists",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_artists_user_id",
                schema: "catalog",
                table: "artists",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_object_key",
                schema: "media",
                table: "media_assets",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_track_id_kind",
                schema: "media",
                table: "media_assets",
                columns: new[] { "track_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracks_artist_id_created_at",
                schema: "catalog",
                table: "tracks",
                columns: new[] { "artist_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_genre_id",
                schema: "catalog",
                table: "tracks",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_new_releases",
                schema: "catalog",
                table: "tracks",
                column: "published_at",
                descending: new bool[0],
                filter: "status = 'ready' AND visibility = 'public' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_upload_jobs_queue",
                schema: "media",
                table: "upload_jobs",
                column: "next_attempt_at",
                filter: "status IN ('uploaded', 'processing')");

            migrationBuilder.CreateIndex(
                name: "ix_upload_jobs_track_id",
                schema: "media",
                table: "upload_jobs",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ux_upload_jobs_active_track",
                schema: "media",
                table: "upload_jobs",
                column: "track_id",
                unique: true,
                filter: "status IN ('uploaded', 'processing')");

            // Cross-module foreign keys (ADR-0001: only to stable identity keys). Added by hand because the
            // referenced entities belong to other modules' EF models.
            migrationBuilder.AddForeignKey(
                name: "fk_artists_users_user_id", schema: "catalog", table: "artists", column: "user_id",
                principalSchema: "identity", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "fk_upload_jobs_tracks_track_id", schema: "media", table: "upload_jobs", column: "track_id",
                principalSchema: "catalog", principalTable: "tracks", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "fk_upload_jobs_users_user_id", schema: "media", table: "upload_jobs", column: "user_id",
                principalSchema: "identity", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "fk_media_assets_tracks_track_id", schema: "media", table: "media_assets", column: "track_id",
                principalSchema: "catalog", principalTable: "tracks", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "fk_media_assets_tracks_track_id", schema: "media", table: "media_assets");
            migrationBuilder.DropForeignKey(name: "fk_upload_jobs_users_user_id", schema: "media", table: "upload_jobs");
            migrationBuilder.DropForeignKey(name: "fk_upload_jobs_tracks_track_id", schema: "media", table: "upload_jobs");
            migrationBuilder.DropForeignKey(name: "fk_artists_users_user_id", schema: "catalog", table: "artists");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "media");

            migrationBuilder.DropTable(
                name: "tracks",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "upload_jobs",
                schema: "media");

            migrationBuilder.DropTable(
                name: "artists",
                schema: "catalog");
        }
    }
}
