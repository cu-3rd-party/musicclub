using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Auth;

[TestFixture]
[TestOf(typeof(ClaimsPrincipalExtensions))]
public class ClaimsPrincipalExtensionsTests
{
    [Test]
    public void GetUserId_WithValidSubClaim_ReturnsGuid()
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        principal.GetUserId()
            .ShouldBe(userId);
    }

    [Test]
    public void GetUserId_WithoutSubClaim_ThrowsUnauthorizedAccessException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Should.Throw<UnauthorizedAccessException>(() => principal.GetUserId());
    }

    [Test]
    public void GetUserId_WithNonSubClaims_ThrowsUnauthorizedAccessException()
    {
        var claims = new[]
        {
            new Claim("email", "test@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        Should.Throw<UnauthorizedAccessException>(() => principal.GetUserId());
    }

    [Test]
    public void GetUserId_WithMultipleClaims_ReturnsSubValue()
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti,
                Guid.NewGuid()
                    .ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("email", "test@example.com"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        principal.GetUserId()
            .ShouldBe(userId);
    }
}
