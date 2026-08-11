using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CuMusicClub.Application.Auth;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Security;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace CuMusicClub.Infrastructure.Services;

public class TelegramAuthService(
    ILogger<TelegramAuthService> logger,
    IOptions<TelegramOptions> telegramOptions,
    ApplicationDbContext db,
    IPermissionService permissionService,
    UserManager<ApplicationUser> userManager,
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

    public User? ExtractTgUser(string initData)
    {
        var parsed = QueryHelpers.ParseQuery(initData);
        if (!parsed.TryGetValue("user", out var userValues))
            throw new BadHttpRequestException("no user in init data string");

        var user = JsonSerializer.Deserialize<User>(userValues.ToString());
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

    public async Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return await authService.RefreshSession(refreshToken, cancellationToken);
    }

    public async Task<TelegramDeeplink> CreateDeeplink(CancellationToken cancellationToken)
    {
        var link = new TgAuthLink();
        db.TgAuthLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return new TelegramDeeplink($"https://t.me/{telegramOptions.Value.BotUsername}?start=auth_{link.Id}", link.Id);
    }

    public async Task<AuthSessionDto?> GetDeeplink(Guid linkUid, CancellationToken cancellationToken)
    {
        var link = await db.TgAuthLinks.FirstOrDefaultAsync(l => l.Id == linkUid, cancellationToken);
        if (link == null || link.TgUserId == null) return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.TgUserId == link.TgUserId, cancellationToken);
        if (user == null) return null;

        db.TgAuthLinks.Remove(link);
        await db.SaveChangesAsync(cancellationToken);

        return await authService.CreateAuthSession(user, cancellationToken);
    }

    public async Task<ApplicationUser> UpsertUserAsync(User tgUser, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.TgUserId == tgUser.Id, cancellationToken);
        if (user != null) return user;

        user = new ApplicationUser
        {
            TgUserId = tgUser.Id,
            UserName = tgUser.Username,
            DisplayName = tgUser.FirstName,
        };
        var result = await userManager.CreateAsync(user);
        logger.LogDebug($"result is {result.Succeeded.ToString()}"); // says result is True, i see the user in the database
        await permissionService.GrantDefaultAsync(user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken); // unneccessary, but doesn't harm
        return user;
    }
}
