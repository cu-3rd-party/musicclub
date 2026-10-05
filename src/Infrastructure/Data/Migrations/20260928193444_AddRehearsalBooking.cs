using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRehearsalBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rehearsal_booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequesterTgUserId = table.Column<long>(type: "bigint", nullable: false),
                    CoachUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SongId = table.Column<Guid>(type: "uuid", nullable: true),
                    cal_dav_event_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    cal_dav_event_etag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    calendar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_bot_managed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rehearsal_booking", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_rehearsal_booking_cal_dav_url",
                table: "rehearsal_booking",
                column: "cal_dav_event_url",
                unique: true,
                filter: "cal_dav_event_url IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_rehearsal_booking_requester_scheduled",
                table: "rehearsal_booking",
                columns: new[] { "RequesterTgUserId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "idx_rehearsal_booking_status_scheduled",
                table: "rehearsal_booking",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rehearsal_booking");
        }
    }
}
