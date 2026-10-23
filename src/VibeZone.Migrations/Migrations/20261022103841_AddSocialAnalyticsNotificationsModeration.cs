using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialAnalyticsNotificationsModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tracks_new_releases",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropIndex(
                name: "ix_playlists_public",
                schema: "playlists",
                table: "playlists");

            migrationBuilder.EnsureSchema(
                name: "moderation");

            migrationBuilder.EnsureSchema(
                name: "social");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.AddColumn<string>(
                name: "takedown_reason",
                schema: "catalog",
                table: "tracks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "taken_down_at",
                schema: "catalog",
                table: "tracks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "taken_down_at",
                schema: "playlists",
                table: "playlists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "moderation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "social",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    timestamp_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_moderation = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.CheckConstraint("ck_comments_body", "char_length(body) BETWEEN 1 AND 1000");
                    table.CheckConstraint("ck_comments_timestamp", "timestamp_ms IS NULL OR timestamp_ms >= 0");
                    table.ForeignKey(
                        name: "fk_comments_comments_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "social",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_reports",
                schema: "moderation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_reports", x => x.id);
                    table.CheckConstraint("ck_content_reports_reason", "reason IN ('copyright', 'abuse', 'spam', 'other')");
                    table.CheckConstraint("ck_content_reports_status", "status IN ('open', 'actioned', 'rejected')");
                    table.CheckConstraint("ck_content_reports_subject_type", "subject_type IN ('track', 'comment', 'playlist')");
                });

            migrationBuilder.CreateTable(
                name: "follows",
                schema: "social",
                columns: table => new
                {
                    follower_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_follows", x => new { x.follower_user_id, x.artist_id });
                });

            migrationBuilder.CreateTable(
                name: "likes",
                schema: "social",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_likes", x => new { x.user_id, x.track_id });
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_type", "type IN ('track_liked', 'artist_followed', 'comment_posted', 'comment_replied', 'track_ready', 'track_failed', 'report_resolved', 'content_removed')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "infra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "play_events",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    position_ms = table.Column<int>(type: "integer", nullable: false),
                    listened_ms = table.Column<int>(type: "integer", nullable: false),
                    client_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_events", x => x.id);
                    table.CheckConstraint("ck_play_events_type", "type IN ('started', 'progress', 'completed')");
                });

            migrationBuilder.CreateTable(
                name: "play_history",
                schema: "analytics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listener_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    listened_ms = table.Column<int>(type: "integer", nullable: false),
                    is_valid_listen = table.Column<bool>(type: "boolean", nullable: false),
                    valid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "track_daily_stats",
                schema: "analytics",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    plays = table.Column<int>(type: "integer", nullable: false),
                    valid_listens = table.Column<int>(type: "integer", nullable: false),
                    likes = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_daily_stats", x => new { x.track_id, x.day });
                    table.CheckConstraint("ck_track_daily_stats_counts", "plays >= 0 AND valid_listens >= 0 AND likes >= 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox_consumptions",
                schema: "infra",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_consumptions", x => new { x.message_id, x.handler });
                    table.ForeignKey(
                        name: "fk_outbox_consumptions_outbox_messages_message_id",
                        column: x => x.message_id,
                        principalSchema: "infra",
                        principalTable: "outbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_new_releases",
                schema: "catalog",
                table: "tracks",
                column: "published_at",
                descending: new bool[0],
                filter: "status = 'ready' AND visibility = 'public' AND deleted_at IS NULL AND taken_down_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_public",
                schema: "playlists",
                table: "playlists",
                column: "updated_at",
                descending: new bool[0],
                filter: "visibility = 'public' AND track_count > 0 AND taken_down_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id_occurred_at",
                schema: "moderation",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                schema: "moderation",
                table: "audit_logs",
                column: "occurred_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_subject_type_subject_id",
                schema: "moderation",
                table: "audit_logs",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_id",
                schema: "social",
                table: "comments",
                column: "parent_id",
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_comments_track_id_created_at",
                schema: "social",
                table: "comments",
                columns: new[] { "track_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_comments_track_id_timestamp_ms",
                schema: "social",
                table: "comments",
                columns: new[] { "track_id", "timestamp_ms" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_status_created_at",
                schema: "moderation",
                table: "content_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_subject_type_subject_id",
                schema: "moderation",
                table: "content_reports",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ux_content_reports_open_per_reporter",
                schema: "moderation",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "subject_type", "subject_id" },
                unique: true,
                filter: "status = 'open'");

            migrationBuilder.CreateIndex(
                name: "ix_follows_artist_id",
                schema: "social",
                table: "follows",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "ix_follows_follower_user_id_created_at",
                schema: "social",
                table: "follows",
                columns: new[] { "follower_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_likes_track_id_created_at",
                schema: "social",
                table: "likes",
                columns: new[] { "track_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_likes_user_id_created_at",
                schema: "social",
                table: "likes",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id_created_at",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "recipient_user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_source_event_id_recipient_user_id",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "source_event_id", "recipient_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_unread",
                schema: "notifications",
                table: "notifications",
                column: "recipient_user_id",
                filter: "read_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_due",
                schema: "infra",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_play_events_session_id",
                schema: "analytics",
                table: "play_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_play_events_track_id_received_at",
                schema: "analytics",
                table: "play_events",
                columns: new[] { "track_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "ix_play_history_user_id_started_at",
                schema: "analytics",
                table: "play_history",
                columns: new[] { "user_id", "started_at" },
                descending: new[] { false, true },
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_play_history_valid_listens",
                schema: "analytics",
                table: "play_history",
                columns: new[] { "listener_key", "track_id", "valid_at" },
                filter: "is_valid_listen");

            migrationBuilder.CreateIndex(
                name: "ix_track_daily_stats_day",
                schema: "analytics",
                table: "track_daily_stats",
                column: "day");

            // Cross-module foreign keys (ADR-0001), added by hand: users, tracks and artists belong to other models.
            // Play events/history and daily stats reference tracks too; tracks are soft-deleted, never removed.
            void Fk(string schema, string table, string column, string principalSchema, string principalTable) =>
                migrationBuilder.AddForeignKey(
                    name: $"fk_{table}_{principalTable}_{column}", schema: schema, table: table, column: column,
                    principalSchema: principalSchema, principalTable: principalTable, principalColumn: "id", onDelete: ReferentialAction.Restrict);

            Fk("social", "likes", "user_id", "identity", "users");
            Fk("social", "likes", "track_id", "catalog", "tracks");
            Fk("social", "follows", "follower_user_id", "identity", "users");
            Fk("social", "follows", "artist_id", "catalog", "artists");
            Fk("social", "comments", "author_user_id", "identity", "users");
            Fk("social", "comments", "track_id", "catalog", "tracks");
            Fk("analytics", "play_events", "user_id", "identity", "users");
            Fk("analytics", "play_events", "track_id", "catalog", "tracks");
            Fk("analytics", "play_history", "user_id", "identity", "users");
            Fk("analytics", "play_history", "track_id", "catalog", "tracks");
            Fk("analytics", "track_daily_stats", "track_id", "catalog", "tracks");
            Fk("notifications", "notifications", "recipient_user_id", "identity", "users");
            Fk("notifications", "notifications", "actor_user_id", "identity", "users");
            Fk("moderation", "content_reports", "reporter_user_id", "identity", "users");
            Fk("moderation", "content_reports", "handled_by_user_id", "identity", "users");
            Fk("moderation", "audit_logs", "actor_user_id", "identity", "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "moderation");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "social");

            migrationBuilder.DropTable(
                name: "content_reports",
                schema: "moderation");

            migrationBuilder.DropTable(
                name: "follows",
                schema: "social");

            migrationBuilder.DropTable(
                name: "likes",
                schema: "social");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_consumptions",
                schema: "infra");

            migrationBuilder.DropTable(
                name: "play_events",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "play_history",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "track_daily_stats",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "infra");

            migrationBuilder.DropIndex(
                name: "ix_tracks_new_releases",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropIndex(
                name: "ix_playlists_public",
                schema: "playlists",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "takedown_reason",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "taken_down_at",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "taken_down_at",
                schema: "playlists",
                table: "playlists");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_new_releases",
                schema: "catalog",
                table: "tracks",
                column: "published_at",
                descending: new bool[0],
                filter: "status = 'ready' AND visibility = 'public' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_public",
                schema: "playlists",
                table: "playlists",
                column: "updated_at",
                descending: new bool[0],
                filter: "visibility = 'public' AND track_count > 0");
        }
    }
}
