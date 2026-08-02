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
using CuMusicClub.Infrastructure.Identity;
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

        var (legacy, identity) = await SyncUsersAsync(telegramUser, isChatMember, cancellationToken);

        var (accessToken, refreshToken, expiresAt) = await IssueTokensAsync(identity, legacy.Id, removeAll: true, cancellationToken);

        return new AuthSessionDto(
            accessToken,
            refreshToken,
            expiresAt,
            DateTimeOffset.UtcNow,
            await BuildProfileAsync(legacy, identity, cancellationToken));
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

        var legacy = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        var identity = await _userManager.Users
            .FirstOrDefaultAsync(u => u.TgUserId == legacy.TgUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        _db.Remove(stored);
        await _db.SaveChangesAsync(cancellationToken);

        var (accessToken, newRefreshToken, expiresAt) =
            await IssueTokensAsync(identity, legacy.Id, removeAll: false, cancellationToken);

        return new TokenPairDto(accessToken, newRefreshToken, expiresAt);
    }

    private async Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)> IssueTokensAsync(
        ApplicationUser identity, Guid appUserId, bool removeAll, CancellationToken cancellationToken)
    {
        var principal = await _claimsFactory.CreateAsync(identity);
        principal.AddIdentity(new ClaimsIdentity(
        [
            new Claim(AppUserClaimTypes.AppUserId, appUserId.ToString()),
        ]));

        var options = _bearerTokenOptions.Get(IdentityConstants.BearerScheme);
        var expiresAt = DateTimeOffset.UtcNow.Add(AccessTokenExpiration);

        var ticket = new AuthenticationTicket(principal, IdentityConstants.BearerScheme);
        ticket.Properties.ExpiresUtc = expiresAt;
        var accessToken = options.BearerTokenProtector.Protect(ticket);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var stale = removeAll
            ? await _db.RefreshTokens.Where(t => t.UserId == appUserId).ToListAsync(cancellationToken)
            : [];
        foreach (var item in stale)
        {
            _db.Remove(item);
        }

        _db.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = appUserId,
            Token = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenExpiration),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (accessToken, refreshToken, expiresAt);
    }

    private async Task<UserProfileDto> BuildProfileAsync(AppUser legacy, ApplicationUser identity, CancellationToken cancellationToken)
    {
        var permissions = await _db.UserPermissions.FirstOrDefaultAsync(p => p.UserId == legacy.Id, cancellationToken);

        return new UserProfileDto(
            legacy.Id,
            legacy.Email,
            legacy.DisplayName,
            permissions is { EditAnySongs: true } or { EditFeaturedSongs: true } ? "admin" : "guest",
            identity.EmailConfirmed,
            legacy.AvatarUrl,
            null,
            legacy.CreatedAt,
            legacy.UpdatedAt);
    }

    private async Task<(AppUser Legacy, ApplicationUser Identity)> SyncUsersAsync(
        TelegramUser telegramUser, bool isChatMember, CancellationToken cancellationToken)
    {
        var displayName = string.IsNullOrWhiteSpace(telegramUser.LastName)
            ? telegramUser.FirstName
            : $"{telegramUser.FirstName} {telegramUser.LastName}";

        var legacy = await _db.AppUsers.FirstOrDefaultAsync(u => u.TgUserId == telegramUser.Id, cancellationToken);

        if (legacy is null)
        {
            var username = await ResolveUniqueUsernameAsync(telegramUser, cancellationToken);

            legacy = new AppUser
            {
                Username = username,
                DisplayName = displayName,
                AvatarUrl = string.IsNullOrWhiteSpace(telegramUser.PhotoUrl) ? null : telegramUser.PhotoUrl,
                TgUserId = telegramUser.Id,
                IsChatMember = isChatMember,
            };
            _db.Add(legacy);
            await _db.SaveChangesAsync(cancellationToken);

            _db.Add(new UserPermission { UserId = legacy.Id, EditOwnParticipation = true, EditOwnSongs = true });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            legacy.DisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(telegramUser.PhotoUrl))
            {
                legacy.AvatarUrl = telegramUser.PhotoUrl;
            }
            legacy.IsChatMember = isChatMember;
        }

        var identity = await _userManager.Users.FirstOrDefaultAsync(u => u.TgUserId == telegramUser.Id, cancellationToken);

        if (identity is null)
        {
            identity = new ApplicationUser { UserName = legacy.Username, TgUserId = telegramUser.Id };
            var result = await _userManager.CreateAsync(identity);
            if (!result.Succeeded)
            {
                identity = new ApplicationUser { UserName = $"tg_{telegramUser.Id}", TgUserId = telegramUser.Id };
                result = await _userManager.CreateAsync(identity);
            }
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }

        return (legacy, identity);
    }

    private async Task<string> ResolveUniqueUsernameAsync(TelegramUser telegramUser, CancellationToken cancellationToken)
    {
        var preferred = string.IsNullOrWhiteSpace(telegramUser.Username)
            ? $"tg_{telegramUser.Id}"
            : telegramUser.Username;

        var clash = await _db.AppUsers.AnyAsync(
            u => u.Username == preferred && u.TgUserId != telegramUser.Id, cancellationToken);

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
