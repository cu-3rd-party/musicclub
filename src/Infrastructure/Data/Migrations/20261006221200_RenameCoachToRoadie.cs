using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCoachToRoadie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the column from CoachUserId to roadie_user_id
            // The column name defaults to the property name if not specified, so it's "CoachUserId"
            migrationBuilder.RenameColumn(
                name: "CoachUserId",
                table: "rehearsal_booking",
                newName: "roadie_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename back from roadie_user_id to CoachUserId
            migrationBuilder.RenameColumn(
                name: "roadie_user_id",
                table: "rehearsal_booking",
                newName: "CoachUserId");
        }
    }
}
