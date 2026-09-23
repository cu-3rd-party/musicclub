using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using ApplicationUser = CuMusicClub.Domain.Entities.ApplicationUser;
using TelegramUser = Telegram.Bot.Types.User;

namespace CuMusicClub.Application.Services.Telegram;

public class TelegramAuthService(
    ILogger<TelegramAuthService> logger,
    IOptions<TelegramOptions> telegramOptions,
    ITgAuthLinkRepository tgAuthLinks,
    IApplicationUserRepository users,
    IPermissionService permissionService,
    IAuthService authService) : ITelegramAuthService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);

    public void Validate(string initData)
    {
        var parsed = QueryHelpers.ParseQuery(initData);
        if (!parsed.TryGetValue("hash", out var hashValues))
            throw new BadHttpRequestException("no hash in init data string");

        var receivedHash = hashValues.ToString();

        var dataCheckString = string.Join("\n",
            parsed
                .Where(x => x.Key != "hash")
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}"));

        byte[] secretKey;
        using (var hmac = new HMACSHA256("WebAppData"u8.ToArray()))
            secretKey = hmac.ComputeHash(Encoding.UTF8.GetBytes(telegramOptions.Value.BotToken));

        byte[] calculatedHash;
        using (var hmac = new HMACSHA256(secretKey))
            calculatedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));

        var receivedHashBytes = Convert.FromHexString(receivedHash);

        if (!CryptographicOperations.FixedTimeEquals(calculatedHash, receivedHashBytes))
            throw new BadHttpRequestException("token hash doesn't match");

        if (!parsed.TryGetValue("auth_date", out var authDateValue))
            throw new BadHttpRequestException("Missing auth_date");

        if (!long.TryParse(authDateValue, out var unixSeconds)) throw new BadHttpRequestException("Invalid auth_date");

        var authDate = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        if (DateTimeOffset.UtcNow - authDate > TokenTtl) throw new BadHttpRequestException("token expired");
    }

    public TelegramUser? ExtractTgUser(string initData)
    {
        var parsed = QueryHelpers.ParseQuery(initData);
        if (!parsed.TryGetValue("user", out var userValues))
            throw new BadHttpRequestException("no user in init data string");

        var userJson = Uri.UnescapeDataString(userValues.ToString());

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var user = JsonSerializer.Deserialize<TelegramUser>(userJson, options);

        if (user == null) throw new BadHttpRequestException("Failed to deserialize user data");

        return user;
    }

    public async Task<AuthSessionDto> AuthenticateAsync(string initData, CancellationToken cancellationToken)
    {
        Validate(initData);
        var tgUser = ExtractTgUser(initData);
        if (tgUser == null) throw new BadHttpRequestException("no user extracted");

        var user = await UpsertUserAsync(tgUser, cancellationToken);
        return await authService.CreateAuthSession(user, cancellationToken);
    }

    public async Task<TelegramDto> CreateDeeplink(CancellationToken cancellationToken)
    {
        var link = new TgAuthLink();
        await tgAuthLinks.AddAsync(link, cancellationToken);
        await tgAuthLinks.SaveChangesAsync(cancellationToken);
        return new TelegramDto($"https://t.me/{telegramOptions.Value.BotUsername}?start=auth_{link.Id}", link.Id);
    }

    public async Task<AuthSessionDto?> GetDeeplink(Guid linkUid, CancellationToken cancellationToken)
    {
        var link = await tgAuthLinks.FindByIdAsync(linkUid, cancellationToken);
        if (link == null || link.TgUserId == null) return null;

        var user = await users.FindByTgUserIdAsync(link.TgUserId.Value, cancellationToken);
        if (user == null) return null;

        tgAuthLinks.Remove(link);
        await tgAuthLinks.SaveChangesAsync(cancellationToken);

        return await authService.CreateAuthSession(user, cancellationToken);
    }

    public async Task<ApplicationUser> UpsertUserAsync(TelegramUser tgUser, CancellationToken cancellationToken)
    {
        var user = await users.FindByTgUserIdAsync(tgUser.Id, cancellationToken);
        if (user != null) return user;

        user = new ApplicationUser
        {
            TgUserId = tgUser.Id,
            UserName = tgUser.Username,
            DisplayName = tgUser.FirstName,
        };
        await users.AddAsync(user, cancellationToken);
        await permissionService.GrantDefaultAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
        return user;
    }
}