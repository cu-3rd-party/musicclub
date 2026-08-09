using CuMusicClub.Application.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
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
}
