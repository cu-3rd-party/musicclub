using System.Security.Cryptography;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace CuMusicClub.Application.Services.Auth;

public class ExternalLoginService(
    IApplicationUserRepository users,
    IUserLoginRepository logins,
    IPermissionService permissionService,
    IAuthService authService,
    IMemoryCache cache) : IExternalLoginService
{
    /// <summary>Сколько живёт код между редиректом с callback и его обменом на сессию.</summary>
    public static readonly TimeSpan LoginCodeLifetime = TimeSpan.FromMinutes(1);

    private const string LoginCodePrefix = "external-login-code:";
    private const int MaxCodeLength = 64;

    public async Task<ApplicationUser> LoginAsync(ExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Subject);

        var now = DateTimeOffset.UtcNow;
        var login = await logins.FindAsync(identity.Provider, identity.Subject, cancellationToken);
        var user = login is null ? null : await users.FindByIdAsync(login.UserId);

        if (user is null)
        {
            // Аккаунт мог появиться раньше — через бота или Telegram Mini App.
            if (identity.TelegramUserId is { } tgUserId)
                user = await users.FindByTgUserIdAsync(tgUserId, cancellationToken);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    TgUserId = identity.TelegramUserId,
                    UserName = NullIfBlank(identity.Username),
                    DisplayName = NullIfBlank(identity.DisplayName) ?? NullIfBlank(identity.Username) ?? string.Empty,
                };
                await users.AddAsync(user, cancellationToken);
                await permissionService.GrantDefaultAsync(user, cancellationToken);
            }

            login = new UserLogin
            {
                UserId = user.Id,
                Provider = UserLogin.NormalizeProvider(identity.Provider),
                Subject = identity.Subject,
                CreatedAt = now,
            };
            await logins.AddAsync(login, cancellationToken);
        }

        await FillMissingAsync(user, identity, cancellationToken);
        login!.LastUsedAt = now;
        await users.SaveChangesAsync(cancellationToken);
        return user;
    }

    public string IssueLoginCode(Guid userId)
    {
        var code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        cache.Set(LoginCodePrefix + code, userId, LoginCodeLifetime);
        return code;
    }

    public async Task<AuthSessionDto?> RedeemLoginCodeAsync(string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength)
            return null;

        var key = LoginCodePrefix + code;
        if (!cache.TryGetValue(key, out Guid userId))
            return null;
        cache.Remove(key);

        var user = await users.FindByIdAsync(userId);
        return user is null ? null : await authService.CreateAuthSession(user, cancellationToken);
    }

    /// <summary>Заполняет пустые поля пользователя данными провайдера; заданное пользователем не трогаем.</summary>
    private async Task FillMissingAsync(ApplicationUser user,
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        if (user.TgUserId is null && identity.TelegramUserId is { } tgUserId &&
            await users.FindByTgUserIdAsync(tgUserId, cancellationToken) is null)
            user.TgUserId = tgUserId;

        if (string.IsNullOrWhiteSpace(user.UserName) && NullIfBlank(identity.Username) is { } username)
            user.UserName = username;

        if (string.IsNullOrWhiteSpace(user.DisplayName) &&
            (NullIfBlank(identity.DisplayName) ?? NullIfBlank(identity.Username)) is { } displayName)
            user.DisplayName = displayName;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
