using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_id",
                schema: "identity",
                table: "user_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_images",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_images", x => x.id);
                    table.CheckConstraint("ck_user_images_kind", "kind IN ('avatarsmall', 'avatarlarge')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_images_object_key",
                schema: "media",
                table: "user_images",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_images_user_id_kind",
                schema: "media",
                table: "user_images",
                columns: new[] { "user_id", "kind" },
                unique: true);

            // Cross-module foreign key (ADR-0001), added by hand: users belong to the Identity model.
            migrationBuilder.AddForeignKey(
                name: "fk_user_images_users_user_id", schema: "media", table: "user_images", column: "user_id",
                principalSchema: "identity", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_images",
                schema: "media");

            migrationBuilder.DropColumn(
                name: "avatar_id",
                schema: "identity",
                table: "user_profiles");
        }
    }
}
