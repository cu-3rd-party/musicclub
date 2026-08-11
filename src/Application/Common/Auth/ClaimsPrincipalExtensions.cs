using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CuMusicClub.Application.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated <c>ApplicationUser.Id</c> from the standard
    /// <see cref="ClaimTypes.NameIdentifier"/> claim.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new UnauthorizedAccessException();
        return Guid.Parse(value);
    }
}
