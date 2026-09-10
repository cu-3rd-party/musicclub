using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Для всех пользователей, у которых ещё нет настроек приватности,
            // создаём предпочтения по умолчанию: allowAdding/allowRemoving = true.
            migrationBuilder.Sql(
                """
                INSERT INTO user_preferences (user_id, allow_adding, allow_removing)
                SELECT "Id", true, true
                FROM app_user
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM user_preferences up
                    WHERE up.user_id = app_user."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
