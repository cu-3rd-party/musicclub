using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRehearsalReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rehearsal_report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rehearsal_report", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rehearsal_report_delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tg_user_id = table.Column<long>(type: "bigint", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rehearsal_report_delivery", x => x.id);
                    table.ForeignKey(
                        name: "FK_rehearsal_report_delivery_rehearsal_report_report_id",
                        column: x => x.report_id,
                        principalTable: "rehearsal_report",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "rehearsal_report_period_unique",
                table: "rehearsal_report",
                column: "period_start",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "rehearsal_report_delivery_unique",
                table: "rehearsal_report_delivery",
                columns: new[] { "report_id", "recipient" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rehearsal_report_delivery");

            migrationBuilder.DropTable(
                name: "rehearsal_report");
        }
    }
}
