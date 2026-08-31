using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    private static async Task<Results<Ok<TokenPairDto>, BadRequest>> Refresh(
        IAuthService authService,
        ITelegramAuthService service,
        RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken)) return TypedResults.BadRequest();

        var tokenPair = await authService.RefreshSession(request.RefreshToken, cancellationToken);
        if (tokenPair == null) return TypedResults.BadRequest();
        return TypedResults.Ok(tokenPair);
    }
}
