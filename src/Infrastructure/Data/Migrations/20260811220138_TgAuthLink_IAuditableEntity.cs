using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TgAuthLink_IAuditableEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "song_role_exists",
                table: "song_role_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_song_role",
                table: "song_role");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Created",
                table: "tg_auth_link",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "tg_auth_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModified",
                table: "tg_auth_link",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifiedBy",
                table: "tg_auth_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "role",
                table: "song_role_assignment",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "song_role",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_song_role",
                table: "song_role",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_song_role_assignment_role",
                table: "song_role_assignment",
                column: "role",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "song_role_song_id_role_title_unique",
                table: "song_role",
                columns: new[] { "song_id", "role" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "song_role_assignment",
                table: "song_role_assignment",
                column: "role",
                principalTable: "song_role",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "song_role_assignment",
                table: "song_role_assignment");

            migrationBuilder.DropIndex(
                name: "IX_song_role_assignment_role",
                table: "song_role_assignment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_song_role",
                table: "song_role");

            migrationBuilder.DropIndex(
                name: "song_role_song_id_role_title_unique",
                table: "song_role");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "tg_auth_link");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "tg_auth_link");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "tg_auth_link");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "tg_auth_link");

            migrationBuilder.DropColumn(
                name: "id",
                table: "song_role");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "song_role_assignment",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddPrimaryKey(
                name: "PK_song_role",
                table: "song_role",
                columns: new[] { "song_id", "role" });

            migrationBuilder.AddForeignKey(
                name: "song_role_exists",
                table: "song_role_assignment",
                columns: new[] { "song_id", "role" },
                principalTable: "song_role",
                principalColumns: new[] { "song_id", "role" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
