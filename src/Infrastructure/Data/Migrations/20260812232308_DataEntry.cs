using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DataEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_entry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false, comment: "полезная информация"),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "этому нельзя доверять, но мы будем"),
                    Hash = table.Column<byte[]>(type: "bytea", nullable: false, comment: "дедубликация"),
                    Size = table.Column<long>(type: "bigint", nullable: false, comment: "опциональная метаинфа")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_entry", x => x.Id);
                },
                comment: "айноды с каким-то содержанием. жалкая замена s3 ибо мне лень инфру настраивать");

            migrationBuilder.CreateIndex(
                name: "IX_data_entry_Hash",
                table: "data_entry",
                column: "Hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_entry");
        }
    }
}
