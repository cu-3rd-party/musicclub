using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoadieAutoAssign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'roadie.auto_assign'
                FROM public.user_permissions
                WHERE "Permission" = 'roadie.manage'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM public.user_permissions
                WHERE "Permission" = 'roadie.auto_assign';
                """);
        }
    }
}
