using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTgAuthLinkWithUserLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tg_auth_link");

            migrationBuilder.CreateTable(
                name: "user_login",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "Claim sub провайдера"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_login", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_login_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Привязки пользователей к внешним провайдерам входа (OIDC)");

            migrationBuilder.CreateIndex(
                name: "idx_user_login_user_id",
                table: "user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_login_provider_subject",
                table: "user_login",
                columns: new[] { "provider", "subject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_login");

            migrationBuilder.CreateTable(
                name: "tg_auth_link",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TgUserId = table.Column<long>(type: "bigint", nullable: true, comment: "Айди кто использовал ссылку")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tg_auth_link", x => x.id);
                },
                comment: "Диплинки для захода в систему");

            migrationBuilder.CreateIndex(
                name: "IX_tg_auth_link_TgUserId",
                table: "tg_auth_link",
                column: "TgUserId");
        }
    }
}
