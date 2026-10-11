using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CuMusicClub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class GrantSongsDeleteToRoadieManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Всем, у кого есть roadie.manage, выдаём songs.delete
            migrationBuilder.Sql("""
                INSERT INTO user_permissions ("UserId", "Permission")
                SELECT "UserId", 'songs.delete'
                FROM user_permissions
                WHERE "Permission" = 'roadie.manage'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM user_permissions WHERE "Permission" = 'songs.delete';
                """);
        }
    }
}
