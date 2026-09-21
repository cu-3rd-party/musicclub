using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipationEditAny1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_override_1'
                FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_any'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;

                DELETE FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override';

                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_override'
                FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override_1'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;

                DELETE FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override_1';

                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_any'
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
                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_own'
                FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_any'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;

                INSERT INTO public.user_permissions ("UserId", "Permission")
                SELECT "UserId", 'participation.edit_any'
                FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;

                DELETE FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_override';

                DELETE FROM public.user_permissions
                WHERE "Permission" = 'participation.edit_any';
                """);
        }
    }
}
