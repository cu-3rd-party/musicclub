using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CuMusicClub.Application.Auth;
using CuMusicClub.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Services;

public class TelegramAuthService(IOptions<TelegramOptions> options) : ITelegramAuthService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);
    private readonly string _botToken = options.Value.BotToken;

    public void Validate(string initData)
    {
        var parsed = QueryHelpers.ParseQuery(initData);
        if (!parsed.TryGetValue("hash", out var hashValues))
        {
            throw new BadHttpRequestException("no hash in init data string");
        }

        var receivedHash = hashValues.ToString();

        var dataCheckString = string.Join("\n",
            parsed
                .Where(x => x.Key != "hash")
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}={x.Value}"));

        byte[] secretKey;
        using (var hmac = new HMACSHA256("WebAppData"u8.ToArray()))
        {
            secretKey = hmac.ComputeHash(Encoding.UTF8.GetBytes(_botToken));
        }

        byte[] calculatedHash;
        using (var hmac = new HMACSHA256(secretKey))
        {
            calculatedHash = hmac.ComputeHash(
                Encoding.UTF8.GetBytes(dataCheckString));
        }

        var receivedHashBytes = Convert.FromHexString(receivedHash);

        if (!CryptographicOperations.FixedTimeEquals(
                calculatedHash,
                receivedHashBytes))
        {
            throw new BadHttpRequestException("token hash doesn't match");
        }

        if (!parsed.TryGetValue("auth_date", out var authDateValue))
        {
            throw new BadHttpRequestException("Missing auth_date");
        }

        if (!long.TryParse(authDateValue, out var unixSeconds))
        {
            throw new BadHttpRequestException("Invalid auth_date");
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        if (DateTimeOffset.UtcNow - authDate > TokenTtl)
        {
            throw new BadHttpRequestException("token expired");
        }
    }

    public Telegram.Bot.Types.User? ExtractTgUser(string initData)
    {
        var parsed = QueryHelpers.ParseQuery(initData);
        if (!parsed.TryGetValue("user", out var userValues))
        {
            throw new BadHttpRequestException("no user in init data string");
        }

        var user = JsonSerializer.Deserialize<Telegram.Bot.Types.User>(userValues.ToString());
        return user;
    }

    public Task<AuthSessionDto> AuthenticateAsync(string initData, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TelegramDeeplink> CreateDeeplink(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<AuthSessionDto?> GetDeeplink(Guid linkUid, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
