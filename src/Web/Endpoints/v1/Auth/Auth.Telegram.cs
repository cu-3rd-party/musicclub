using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Sign in with Telegram WebApp init data")]
    private static async Task<Results<Ok<AuthSessionDto>, BadRequest>> TelegramInitData(ITelegramAuthService service,
        TelegramAuthRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.InitData)) return TypedResults.BadRequest();

        var session = await service.AuthenticateAsync(request.InitData, cancellationToken);

        return TypedResults.Ok(session);
    }

    [EndpointSummary("Request telegram /start deeplink for usage in bot")]
    private static async Task<Results<Ok<TelegramDto>, BadRequest>> TelegramDeeplink(ITelegramAuthService service,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await service.CreateDeeplink(cancellationToken));
    }

    [EndpointSummary("Get auth session from deeplink. One time use")]
    private static async Task<Results<Ok<AuthSessionDto>, NoContent>> LoginDeeplink(ITelegramAuthService service,
        Guid deeplinkUid,
        CancellationToken cancellationToken)
    {
        var authSession = await service.GetDeeplink(deeplinkUid, cancellationToken);

        if (authSession == null) return TypedResults.NoContent();

        return TypedResults.Ok(authSession);
    }
}
