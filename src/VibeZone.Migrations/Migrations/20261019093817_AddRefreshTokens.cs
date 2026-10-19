using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeZone.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "previous_refresh_token_hash",
                schema: "identity",
                table: "user_sessions",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "refresh_token_hash",
                schema: "identity",
                table: "user_sessions",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_previous_refresh_token_hash",
                schema: "identity",
                table: "user_sessions",
                column: "previous_refresh_token_hash",
                filter: "previous_refresh_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_refresh_token_hash",
                schema: "identity",
                table: "user_sessions",
                column: "refresh_token_hash",
                unique: true,
                filter: "refresh_token_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_sessions_previous_refresh_token_hash",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "ix_user_sessions_refresh_token_hash",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "previous_refresh_token_hash",
                schema: "identity",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "refresh_token_hash",
                schema: "identity",
                table: "user_sessions");
        }
    }
}
