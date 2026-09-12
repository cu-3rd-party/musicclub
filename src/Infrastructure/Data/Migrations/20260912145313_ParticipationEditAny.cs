using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipationEditAny : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_override'
                FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_own'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override'
                  AND "UserId" IN (
                      SELECT "UserId"
                      FROM public.user_permissions
                      WHERE "Permission" = 'participation.edit_own'
                  );
                """);
        }
    }
}
