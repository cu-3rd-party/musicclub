using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Sign in with Telegram WebApp init data")]
    private static async Task<Results<Ok<AuthSessionDto>, ProblemHttpResult>> TelegramInitData(ITelegramAuthService service,
        TelegramAuthRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.InitData)) return ApiProblems.BadRequest("Не переданы данные входа Telegram.");

        var session = await service.AuthenticateAsync(request.InitData, cancellationToken);

        return TypedResults.Ok(session);
    }
}
