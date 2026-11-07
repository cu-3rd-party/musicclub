using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    private static async Task<Results<Ok<TokenPairDto>, ProblemHttpResult>> Refresh(
        IAuthService authService,
        ITelegramAuthService service,
        RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken)) return ApiProblems.BadRequest("Не передан refresh-токен.");

        var tokenPair = await authService.RefreshSession(request.RefreshToken, cancellationToken);
        if (tokenPair == null) return ApiProblems.BadRequest("Сессия истекла — войдите ещё раз.");
        return TypedResults.Ok(tokenPair);
    }
}
