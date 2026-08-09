namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/telegram", Telegram);
        group.MapPost("/refresh", Refresh);
    }
}
