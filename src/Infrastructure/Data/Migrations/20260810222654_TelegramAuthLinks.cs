using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TelegramAuthLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tg_auth_link",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tg_auth_link");
        }
    }
}
