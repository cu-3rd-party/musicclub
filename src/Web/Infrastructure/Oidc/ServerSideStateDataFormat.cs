using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace CuMusicClub.Web.Infrastructure.Oidc;

/// <summary>
/// <c>state</c> внешнего входа — короткий случайный ключ, а сами <see cref="AuthenticationProperties"/>
/// (return URL, correlation id, PKCE code_verifier) лежат в кеше на сервере.
/// </summary>
/// <remarks>
/// По умолчанию ASP.NET шифрует свойства прямо в <c>state</c> — это сотни символов, а Telegram отвечает
/// «state too long». Ключ одноразовый и живёт <see cref="Lifetime"/>; кеш в памяти, поэтому вход, начатый
/// до перезапуска бэкенда, придётся начать заново.
/// </remarks>
public class ServerSideStateDataFormat(IDistributedCache cache) : ISecureDataFormat<AuthenticationProperties>
{
    private const string KeyPrefix = "oidc-state:";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public string Protect(AuthenticationProperties data)
    {
        return Protect(data, null);
    }

    public string Protect(AuthenticationProperties data, string? purpose)
    {
        var key = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        cache.Set(CacheKey(key, purpose), PropertiesSerializer.Default.Serialize(data),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime });
        return key;
    }

    public AuthenticationProperties? Unprotect(string? protectedText)
    {
        return Unprotect(protectedText, null);
    }

    public AuthenticationProperties? Unprotect(string? protectedText, string? purpose)
    {
        if (string.IsNullOrEmpty(protectedText) || protectedText.Length > 64)
            return null;

        var cacheKey = CacheKey(protectedText, purpose);
        var data = cache.Get(cacheKey);
        if (data is null)
            return null;

        cache.Remove(cacheKey);
        return PropertiesSerializer.Default.Deserialize(data);
    }

    private static string CacheKey(string key, string? purpose)
    {
        return $"{KeyPrefix}{purpose}:{key}";
    }
}
