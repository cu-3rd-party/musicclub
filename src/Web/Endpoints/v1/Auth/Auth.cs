using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/telegram", TelegramInitData);
        group.MapGet("/providers", Providers);
        group.MapGet("/external/{provider}", ExternalChallenge);
        group.MapGet("/external/{provider}/complete", ExternalComplete);
        group.MapPost("/external/exchange", ExternalExchange);
        group.MapPost("/refresh", Refresh);

        var authed = group
            .MapGroup("/")
            .RequireAuthorization();
        authed.MapGet("/me", Me);
    }
}
