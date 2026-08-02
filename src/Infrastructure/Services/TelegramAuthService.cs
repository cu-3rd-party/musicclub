using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using CuMusicClub.Application.Auth;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Services;

public class TelegramAuthService : ITelegramAuthService
{
    private static readonly TimeSpan AccessTokenExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenExpiration = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _claimsFactory;
    private readonly IOptionsMonitor<BearerTokenOptions> _bearerTokenOptions;
    private readonly TelegramOptions _telegramOptions;
    private readonly HttpClient _httpClient;

    public TelegramAuthService(
        IApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        IOptions<TelegramOptions> telegramOptions,
        HttpClient httpClient)
    {
        _db = db;
        _userManager = userManager;
        _claimsFactory = claimsFactory;
        _bearerTokenOptions = bearerTokenOptions;
        _telegramOptions = telegramOptions.Value;
        _httpClient = httpClient;
    }

    public async Task<AuthSessionDto> AuthenticateAsync(string initData, CancellationToken cancellationToken)
    {
        var telegramUser = VerifyTelegramWebAppData(initData);

        var isChatMember = await IsChatMemberAsync(telegramUser.Id, cancellationToken);

        var user = await SyncUserAsync(telegramUser, isChatMember, cancellationToken);

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(user, removeAll: true, cancellationToken);

        return new AuthSessionDto(
            accessToken,
            refreshToken,
            expiresAt,
            DateTimeOffset.UtcNow,
            await BuildProfileAsync(user, cancellationToken));
    }

    public async Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required");
        }

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (stored is null || stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        _db.Remove(stored);
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, newRefreshToken, expiresAt) =
            await IssueTokensAsync(user, removeAll: false, cancellationToken);

        return new TokenPairDto(accessToken, newRefreshToken, expiresAt);
    }

    private async Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)> IssueTokensAsync(
        ApplicationUser user, bool removeAll, CancellationToken cancellationToken)
    {
        var principal = await _claimsFactory.CreateAsync(user);

        var options = _bearerTokenOptions.Get(IdentityConstants.BearerScheme);
        var expiresAt = DateTimeOffset.UtcNow.Add(AccessTokenExpiration);

        var ticket = new AuthenticationTicket(principal, IdentityConstants.BearerScheme);
        ticket.Properties.ExpiresUtc = expiresAt;
        var accessToken = options.BearerTokenProtector.Protect(ticket);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var stale = removeAll
            ? await _db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync(cancellationToken)
            : [];
        foreach (var item in stale)
        {
            _db.Remove(item);
        }

        _db.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenExpiration),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (accessToken, refreshToken, expiresAt);
    }

    private async Task<UserProfileDto> BuildProfileAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var principal = await _claimsFactory.CreateAsync(user);

        var role = principal.HasPermission(Permissions.SongsEditAny) || principal.HasPermission(Permissions.SongsEditFeatured)
            ? "admin"
            : "guest";

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            role,
            user.EmailConfirmed,
            user.AvatarUrl,
            null,
            user.CreatedAt,
            user.UpdatedAt);
    }

    private async Task<ApplicationUser> SyncUserAsync(
        TelegramUser telegramUser, bool isChatMember, CancellationToken cancellationToken)
    {
        var displayName = string.IsNullOrWhiteSpace(telegramUser.LastName)
            ? telegramUser.FirstName
            : $"{telegramUser.FirstName} {telegramUser.LastName}";

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.TgUserId == telegramUser.Id, cancellationToken);

        if (user is null)
        {
            var username = await ResolveUniqueUsernameAsync(telegramUser, cancellationToken);

            user = new ApplicationUser
            {
                UserName = username,
                DisplayName = displayName,
                AvatarUrl = string.IsNullOrWhiteSpace(telegramUser.PhotoUrl) ? null : telegramUser.PhotoUrl,
                TgUserId = telegramUser.Id,
                IsChatMember = isChatMember,
            };
            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                user.UserName = $"tg_{telegramUser.Id}";
                result = await _userManager.CreateAsync(user);
            }
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddClaimsAsync(user,
            [
                new Claim(PermissionClaimTypes.Permission, Permissions.SongsEditOwn),
                new Claim(PermissionClaimTypes.Permission, Permissions.ParticipationEditOwn),
            ]);
        }
        else
        {
            user.DisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(telegramUser.PhotoUrl))
            {
                user.AvatarUrl = telegramUser.PhotoUrl;
            }
            user.IsChatMember = isChatMember;
            await _userManager.UpdateAsync(user);
        }

        return user;
    }

    private async Task<string> ResolveUniqueUsernameAsync(TelegramUser telegramUser, CancellationToken cancellationToken)
    {
        var preferred = string.IsNullOrWhiteSpace(telegramUser.Username)
            ? $"tg_{telegramUser.Id}"
            : telegramUser.Username;

        var clash = await _userManager.Users.AnyAsync(
            u => u.UserName == preferred && u.TgUserId != telegramUser.Id, cancellationToken);

        return clash ? $"tg_{telegramUser.Id}" : preferred;
    }

    private async Task<bool> IsChatMemberAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        if (_telegramOptions.SkipChatMembershipCheck)
        {
            return true;
        }

        var url = $"https://api.telegram.org/bot{_telegramOptions.BotToken}/getChatMember" +
                  $"?chat_id={_telegramOptions.ChatId}&user_id={telegramUserId}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to check chat membership");
        }

        var payload = await response.Content.ReadFromJsonAsync<ChatMemberResponse>(cancellationToken);
        return payload is { Ok: true } && payload.Result.Status is "creator" or "administrator" or "member";
    }

    private TelegramUser VerifyTelegramWebAppData(string initData)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }
            var key = Decode(pair[..separator]);
            var value = Decode(pair[(separator + 1)..]);
            values[key] = value;
        }

        if (!values.TryGetValue("hash", out var hash) || string.IsNullOrEmpty(hash))
        {
            throw new UnauthorizedAccessException("Invalid Telegram data");
        }

        values.Remove("hash");

        var dataCheckString = string.Join("\n", values
            .Select(pair => $"{pair.Key}={pair.Value}")
            .OrderBy(s => s, StringComparer.Ordinal));

        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(_telegramOptions.BotToken));
        var computedHash = Convert.ToHexString(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHash), Encoding.UTF8.GetBytes(hash)))
        {
            throw new UnauthorizedAccessException("Invalid Telegram data");
        }

        if (!values.TryGetValue("user", out var userJson) || string.IsNullOrEmpty(userJson))
        {
            throw new UnauthorizedAccessException("Invalid Telegram data");
        }

        var user = JsonSerializer.Deserialize<TelegramUser>(userJson, JsonOptions)
            ?? throw new UnauthorizedAccessException("Invalid Telegram data");

        if (string.IsNullOrWhiteSpace(_telegramOptions.BotToken))
        {
            throw new InvalidOperationException("Telegram BotToken is not configured");
        }

        return user;
    }

    private static string Decode(string value)
    {
        var replaced = value.Replace('+', ' ');
        return Uri.UnescapeDataString(replaced);
    }

    private sealed record TelegramUser(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string? LastName,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("photo_url")] string? PhotoUrl);

    private sealed class ChatMemberResponse
    {
        public bool Ok { get; set; }
        public ChatMemberResult Result { get; set; } = new();
    }

    private sealed class ChatMemberResult
    {
        public string Status { get; set; } = string.Empty;
    }
}
