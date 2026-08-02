using CuMusicClub.Application.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public static class Auth
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/telegram", Telegram);
        group.MapPost("/refresh", Refresh);
    }

    [EndpointSummary("Sign in with Telegram WebApp init data")]
    private static async Task<Results<Ok<AuthSessionDto>, BadRequest>> Telegram(
        ITelegramAuthService service, TelegramAuthRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.InitData))
        {
            return TypedResults.BadRequest();
        }

        var session = await service.AuthenticateAsync(request.InitData, cancellationToken);

        return TypedResults.Ok(session);
    }

    [EndpointSummary("Exchange a refresh token for a new token pair")]
    private static async Task<Results<Ok<TokenPairDto>, BadRequest>> Refresh(
        ITelegramAuthService service, RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return TypedResults.BadRequest();
        }

        var pair = await service.RefreshAsync(request.RefreshToken, cancellationToken);

        return TypedResults.Ok(pair);
    }
}
