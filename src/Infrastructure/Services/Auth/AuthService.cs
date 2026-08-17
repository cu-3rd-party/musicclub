using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CuMusicClub.Infrastructure.Services.Auth;

public class AuthService(IOptions<SecurityOptions> securityOptions, IPermissionService permissionService) : IAuthService
{
    private static readonly TimeSpan AccessTokenTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(7);

    private readonly SymmetricSecurityKey _signingKey = securityOptions.Value.SigningKey;

    public async Task<AuthSessionDto> CreateAuthSession(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,
                Guid
                    .NewGuid()
                    .ToString()),
        };

        var accessToken = CreateToken(claims, now, AccessTokenTtl);
        var refreshToken = CreateToken(claims, now, RefreshTokenTtl);

        var profile = new UserProfileDto(user.Id,
            user.DisplayName,
            user.UserName ?? string.Empty,
            user.AvatarUrl,
            await permissionService.GetPermissionValuesAsync(user, cancellationToken),
            null,
            user.CreatedAt,
            user.UpdatedAt);

        var session = new AuthSessionDto(accessToken, refreshToken, now + AccessTokenTtl, now, profile);
        // TODO: это надо добавлять в user_session и в дальнейшем сделать апи менеджмента сессиями
        return session;
    }

    public Task<TokenPairDto> RefreshSession(string refreshToken, CancellationToken cancellationToken)
    {
        var handler = new JwtSecurityTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        var principal = handler.ValidateToken(refreshToken, parameters, out _);

        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)
            ?.Value;

        if (string.IsNullOrWhiteSpace(userId)) throw new SecurityTokenException("Refresh token has no sub claim.");

        var now = DateTimeOffset.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti,
                Guid
                    .NewGuid()
                    .ToString()),
        };

        var newAccessToken = CreateToken(claims, now, AccessTokenTtl);
        var newRefreshToken = CreateToken(claims, now, RefreshTokenTtl);

        return Task.FromResult(new TokenPairDto(newAccessToken, newRefreshToken, now + AccessTokenTtl));
    }

    private string CreateToken(IEnumerable<Claim> claims, DateTimeOffset issuedAt, TimeSpan ttl)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt.UtcDateTime,
            Expires = (issuedAt + ttl).UtcDateTime,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
