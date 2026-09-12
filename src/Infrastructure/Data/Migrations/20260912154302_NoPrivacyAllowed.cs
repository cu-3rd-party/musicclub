using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultPreferencesForAllUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO public.user_preferences (user_id, allow_adding, allow_removing)
                SELECT u.""Id"", true, true
                FROM public.app_user u
                ON CONFLICT (user_id)
                DO UPDATE SET
                    allow_adding = true,
                    allow_removing = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE public.user_preferences
                SET allow_adding = true,
                    allow_removing = true;
            ");
        }
    }
}
