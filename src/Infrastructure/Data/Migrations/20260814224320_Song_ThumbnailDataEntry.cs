using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Song_ThumbnailDataEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "thumbnail_url",
                table: "song",
                type: "text",
                nullable: true,
                comment: "DEPRECATED: SHOULD BE READONLY. USE thumbnail_data_entry_id",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "thumbnail_data_entry_id",
                table: "song",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_song_thumbnail_data_entry_id",
                table: "song",
                column: "thumbnail_data_entry_id");

            migrationBuilder.AddForeignKey(
                name: "FK_song_data_entry_thumbnail_data_entry_id",
                table: "song",
                column: "thumbnail_data_entry_id",
                principalTable: "data_entry",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_song_data_entry_thumbnail_data_entry_id",
                table: "song");

            migrationBuilder.DropIndex(
                name: "IX_song_thumbnail_data_entry_id",
                table: "song");

            migrationBuilder.DropColumn(
                name: "thumbnail_data_entry_id",
                table: "song");

            migrationBuilder.AlterColumn<string>(
                name: "thumbnail_url",
                table: "song",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "DEPRECATED: SHOULD BE READONLY. USE thumbnail_data_entry_id");
        }
    }
}
