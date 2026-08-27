using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SongTopic_SongTopicMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_song_topic",
                table: "song_topic");

            migrationBuilder.DropIndex(
                name: "idx_song_topic_topic_id",
                table: "song_topic");

            migrationBuilder.AlterColumn<long>(
                name: "topic_id",
                table: "song_topic",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "song_topic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "song_topic",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_song_topic",
                table: "song_topic",
                column: "topic_id");

            migrationBuilder.CreateTable(
                name: "song_topic_member",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_song_topic_member", x => x.id);
                    table.ForeignKey(
                        name: "FK_song_topic_member_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_song_topic_member_song_topic_topic_id",
                        column: x => x.topic_id,
                        principalTable: "song_topic",
                        principalColumn: "topic_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_song_topic_song_id",
                table: "song_topic",
                column: "song_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_song_topic_member_topic_id",
                table: "song_topic_member",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "IX_song_topic_member_user_id",
                table: "song_topic_member",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "song_topic_member");

            migrationBuilder.DropPrimaryKey(
                name: "PK_song_topic",
                table: "song_topic");

            migrationBuilder.DropIndex(
                name: "idx_song_topic_song_id",
                table: "song_topic");

            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "song_topic");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "song_topic");

            migrationBuilder.AlterColumn<long>(
                name: "topic_id",
                table: "song_topic",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_song_topic",
                table: "song_topic",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "idx_song_topic_topic_id",
                table: "song_topic",
                column: "topic_id");
        }
    }
}
