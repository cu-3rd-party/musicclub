namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/telegram", TelegramInitData);
        group.MapGet("/telegram/link", TelegramDeeplink);
        group.MapGet("/telegram/link/{deeplinkUid:guid}", LoginDeeplink);
        group.MapPost("/refresh", Refresh);
    }
}
