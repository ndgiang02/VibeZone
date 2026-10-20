using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumsAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "album_id",
                schema: "catalog",
                table: "tracks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "track_number",
                schema: "catalog",
                table: "tracks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "albums",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    artist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_albums", x => x.id);
                    table.CheckConstraint("ck_albums_kind", "kind IN ('album', 'ep', 'single')");
                    table.CheckConstraint("ck_albums_visibility", "visibility IN ('public', 'unlisted', 'private')");
                    table.ForeignKey(
                        name: "fk_albums_artist_artist_id",
                        column: x => x.artist_id,
                        principalSchema: "catalog",
                        principalTable: "artists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_album_id_track_number",
                schema: "catalog",
                table: "tracks",
                columns: new[] { "album_id", "track_number" },
                filter: "album_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_albums_artist_id_created_at",
                schema: "catalog",
                table: "albums",
                columns: new[] { "artist_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "fk_tracks_albums_album_id",
                schema: "catalog",
                table: "tracks",
                column: "album_id",
                principalSchema: "catalog",
                principalTable: "albums",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Search (EfSearchQueries). Not in the EF model: generated columns EF never writes.
            // pg_trgm and unaccent are trusted extensions (PG13+), so the database owner can create them.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS unaccent WITH SCHEMA public;
                CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;

                -- lower + strip diacritics ("Sơn Tùng" -> "son tung", "đ" -> "d"). Immutable wrapper (unaccent
                -- itself is only stable) so it can back generated columns and expression indexes.
                CREATE OR REPLACE FUNCTION public.vz_fold(text) RETURNS text
                    LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                    AS $$ SELECT lower(public.unaccent('public.unaccent'::regdictionary, $1)) $$;

                ALTER TABLE catalog.tracks ADD COLUMN search_vector tsvector GENERATED ALWAYS AS
                    (to_tsvector('simple', public.vz_fold(title || ' ' || coalesce(description, '')))) STORED;
                ALTER TABLE catalog.artists ADD COLUMN search_vector tsvector GENERATED ALWAYS AS
                    (to_tsvector('simple', public.vz_fold(name || ' ' || slug))) STORED;
                ALTER TABLE catalog.albums ADD COLUMN search_vector tsvector GENERATED ALWAYS AS
                    (to_tsvector('simple', public.vz_fold(title || ' ' || coalesce(description, '')))) STORED;

                CREATE INDEX ix_tracks_search ON catalog.tracks USING gin (search_vector);
                CREATE INDEX ix_artists_search ON catalog.artists USING gin (search_vector);
                CREATE INDEX ix_albums_search ON catalog.albums USING gin (search_vector);
                CREATE INDEX ix_tracks_title_trgm ON catalog.tracks USING gin (public.vz_fold(title) public.gin_trgm_ops);
                CREATE INDEX ix_artists_name_trgm ON catalog.artists USING gin (public.vz_fold(name) public.gin_trgm_ops);
                CREATE INDEX ix_albums_title_trgm ON catalog.albums USING gin (public.vz_fold(title) public.gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE catalog.tracks DROP COLUMN IF EXISTS search_vector;
                ALTER TABLE catalog.artists DROP COLUMN IF EXISTS search_vector;
                DROP INDEX IF EXISTS catalog.ix_tracks_title_trgm;
                DROP INDEX IF EXISTS catalog.ix_artists_name_trgm;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_tracks_albums_album_id",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropTable(
                name: "albums",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_tracks_album_id_track_number",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "album_id",
                schema: "catalog",
                table: "tracks");

            migrationBuilder.DropColumn(
                name: "track_number",
                schema: "catalog",
                table: "tracks");
        }
    }
}
