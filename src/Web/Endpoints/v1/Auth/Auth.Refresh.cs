using CuMusicClub.Application.Auth;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Exchange a refresh token for a new token pair")]
    private static async Task<Results<Ok<TokenPairDto>, BadRequest>> Refresh(ITelegramAuthService service,
        RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return TypedResults.BadRequest();
        }

        var tokenPair = await service.RefreshAsync(request.RefreshToken, cancellationToken);
        return TypedResults.Ok(tokenPair);
    }
}
