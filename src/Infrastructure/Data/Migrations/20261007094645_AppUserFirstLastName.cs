using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppUserFirstLastName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "app_user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "app_user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "app_user");
        }
    }
}
