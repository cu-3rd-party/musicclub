namespace CuMusicClub.Web.Endpoints.v1.Users;

public static partial class Users
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/{userId:guid}", Get);
        group.MapGet("/me/preferences", GetPreferences);
        group.MapPut("/me/preferences", UpdatePreferences);
        group.MapGet("/me/assignments", GetMyAssignments);
        group.MapGet("/me/roadie/assignments", GetMyRoadieAssignments);
    }
}
