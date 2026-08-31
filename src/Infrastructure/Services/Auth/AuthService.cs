using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CuMusicClub.Infrastructure.Services.Auth;

public class AuthService(
    IOptions<SecurityOptions> securityOptions,
    ILogger<AuthService> logger,
    ApplicationDbContext db,
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    private static readonly TimeSpan AccessTokenTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(7);

    private readonly SymmetricSecurityKey _signingKey = securityOptions.Value.SigningKey;

    public async Task<AuthSessionDto> CreateAuthSession(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshTokenObj = new RefreshToken
        {
            Jti = Guid.NewGuid(),
            Sub = user.Id,
            Exp = now + RefreshTokenTtl,
            Iat = now,
        };
        var httpContext = httpContextAccessor.HttpContext;
        var headers = httpContext?.Request.Headers;
        var userSession = new UserSession
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            IpAddress = headers?["X-Real-Ip"].FirstOrDefault()
                        ?? headers?["X-Forwarded-For"].FirstOrDefault()?.Split(',').First()?.Trim()
                        ?? httpContext?.Connection.RemoteIpAddress?.ToString(),
            LastActivityAt = now,
            ScreenResolution = headers?["X-Screen-Resolution"].FirstOrDefault(),
            UserId = user.Id,
            User = user,
            UserAgent = headers?.UserAgent.ToString(),
            RefreshTokenJti = refreshTokenObj.Jti,
            RefreshToken = refreshTokenObj,
        };

        var accessClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,
                Guid
                    .NewGuid()
                    .ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "access"),
        };

        var refreshClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, refreshTokenObj.Jti.ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "refresh"),
        };

        var accessToken = CreateToken(accessClaims, now, AccessTokenTtl);
        var refreshToken = CreateToken(refreshClaims,
            now,
            RefreshTokenTtl,
            iat: refreshTokenObj.Iat,
            exp: refreshTokenObj.Exp);

        var profile = new UserProfileDto(user.Id,
            user.DisplayName,
            user.UserName ?? string.Empty,
            user.AvatarUrl,
            await permissionService.GetPermissionValuesAsync(user, cancellationToken),
            null,
            user.CreatedAt,
            user.UpdatedAt);

        var session = new AuthSessionDto(accessToken, refreshToken, now + AccessTokenTtl, now, profile);
        await db.RefreshTokens.AddAsync(refreshTokenObj, cancellationToken);
        await db.UserSessions.AddAsync(userSession, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<TokenPairDto?> RefreshSession(string refreshToken, CancellationToken cancellationToken)
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

        if (principal.FindFirst("typ")
                ?.Value !=
            "refresh")
            return null;

        var jti = principal.FindFirst("jti")
            ?.Value;
        if (jti == null) return null;
        var refreshTokenObj = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Jti == Guid.Parse(jti) && !t.Revoked && t.Exp > DateTimeOffset.UtcNow,
            cancellationToken: cancellationToken);
        if (refreshTokenObj == null) return null;
        var userSession = await db.UserSessions.FirstOrDefaultAsync(s => s.RefreshTokenJti == refreshTokenObj.Jti, cancellationToken);
        if (userSession == null) return null;

        var now = DateTimeOffset.UtcNow;
        var userId = userSession.UserId;

        refreshTokenObj.Revoked = true;
        var newRefreshTokenObj = new RefreshToken
        {
            Jti = Guid.NewGuid(),
            Sub = userId,
            Exp = now + RefreshTokenTtl,
            Iat = now,
        };
        userSession.RefreshTokenJti = newRefreshTokenObj.Jti;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, newRefreshTokenObj.Jti.ToString()),
            new Claim(JwtRegisteredClaimNames.Typ, "refresh"),
        };

        var newAccessToken = CreateToken(claims, now, AccessTokenTtl);
        var newRefreshToken = CreateToken(claims, now, RefreshTokenTtl, iat: newRefreshTokenObj.Iat, exp: newRefreshTokenObj.Exp);

        await db.RefreshTokens.AddAsync(newRefreshTokenObj, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new TokenPairDto(newAccessToken, newRefreshToken, now + AccessTokenTtl);
    }

    private string CreateToken(IEnumerable<Claim> claims,
        DateTimeOffset issuedAt,
        TimeSpan ttl,
        DateTimeOffset? iat = null,
        DateTimeOffset? exp = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = iat?.UtcDateTime ?? issuedAt.UtcDateTime,
            Expires = exp?.UtcDateTime ?? (issuedAt + ttl).UtcDateTime,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
