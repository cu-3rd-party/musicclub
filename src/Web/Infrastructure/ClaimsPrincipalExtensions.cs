using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;

namespace CuMusicClub.Web.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetAppUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppUserClaimTypes.AppUserId);
        if (Guid.TryParse(value, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException();
    }
}
