using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefreshTokenJti",
                table: "user_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    jti = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    sub = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    iat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.jti);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_AspNetUsers_sub",
                        column: x => x.sub,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_session_RefreshTokenJti",
                table: "user_session",
                column: "RefreshTokenJti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_refresh_token_exp",
                table: "refresh_tokens",
                column: "exp");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_token_iat",
                table: "refresh_tokens",
                column: "iat");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_token_revoked",
                table: "refresh_tokens",
                column: "Revoked");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_token_sub",
                table: "refresh_tokens",
                column: "sub");

            migrationBuilder.AddForeignKey(
                name: "FK_user_session_refresh_tokens_RefreshTokenJti",
                table: "user_session",
                column: "RefreshTokenJti",
                principalTable: "refresh_tokens",
                principalColumn: "jti",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_session_refresh_tokens_RefreshTokenJti",
                table: "user_session");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_user_session_RefreshTokenJti",
                table: "user_session");

            migrationBuilder.DropColumn(
                name: "RefreshTokenJti",
                table: "user_session");
        }
    }
}
