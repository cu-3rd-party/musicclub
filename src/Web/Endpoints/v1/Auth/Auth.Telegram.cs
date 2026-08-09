using CuMusicClub.Application.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Sign in with Telegram WebApp init data")]
    private static async Task<Results<Ok<AuthSessionDto>, BadRequest>> TelegramInitData(
        ITelegramAuthService service, TelegramAuthRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.InitData))
        {
            return TypedResults.BadRequest();
        }

        var session = await service.AuthenticateAsync(request.InitData, cancellationToken);

        return TypedResults.Ok(session);
    }

    [EndpointSummary("Request telegram /start deeplink for usage in bot")]
    private static async Task<Results<Ok<TelegramDeeplink>, BadRequest>> TelegramDeeplink(
        ITelegramAuthService service, CancellationToken cancellationToken)
    {
        var link = await service.CreateDeeplink(cancellationToken);
        return TypedResults.Ok(new TelegramDeeplink($"auth_{link.Id}", link.Id));
    }

    [EndpointSummary("Get auth session from deeplink. One time use")]
    private static async Task<Results<Ok<AuthSessionDto>, BadRequest>> LoginDeeplink(
            ITelegramAuthService service, Guid deeplinkUid, CancellationToken cancellationToken)
    {
        var authSession = await service.GetDeeplink(deeplinkUid, cancellationToken);
        if (authSession == null)
        {
            return TypedResults.BadRequest();
        }

        return TypedResults.Ok(authSession);

    }
}
