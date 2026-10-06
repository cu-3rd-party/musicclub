using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Снимок авторизационного состояния для вызовов веб-API Яндекс.Календаря.
/// </summary>
public sealed record YandexWebAuthState(string CookieHeader, string Uid, string CKey);

/// <summary>
///     Держит cookies/ckey для веб-API Яндекс.Календаря и обновляет их через Playwright, когда они протухают.
///     Аналог reload_session() / _get_calendar_ckey() из musicscheduler/yandex_api.py, только с реальным перелогином.
/// </summary>
public interface IYandexWebSession
{
    /// <summary>
    ///     Есть ли чем авторизоваться: файл cookies, cookies в конфиге или логин/пароль для Playwright.
    /// </summary>
    bool IsConfigured { get; }

    Task<YandexWebAuthState> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    ///     Сбрасывает кеш и прогоняет Playwright. Без <paramref name="force" /> не чаще, чем раз в MinRefreshInterval.
    /// </summary>
    Task<bool> RefreshAsync(bool force = false, CancellationToken ct = default);

    /// <summary>
    ///     Сбрасывает закешированные cookies/ckey без запуска браузера — следующий вызов перечитает их.
    /// </summary>
    void Invalidate();
}

public sealed partial class YandexWebSession(
    IHttpClientFactory httpClientFactory,
    IYandexCookieRefresher cookieRefresher,
    IOptions<YandexWebCalendarOptions> options,
    ILogger<YandexWebSession> logger) : IYandexWebSession, IDisposable
{
    public const string HttpClientName = "yandex-web";

    private readonly YandexWebCalendarOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private YandexWebAuthState? _state;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public bool IsConfigured
    {
        get
        {
            return _state != null
                   || File.Exists(Path.GetFullPath(_options.CookiePath))
                   || !string.IsNullOrWhiteSpace(_options.Cookie)
                   || (!string.IsNullOrWhiteSpace(_options.Login) && !string.IsNullOrWhiteSpace(_options.Password));
        }
    }

    public async Task<YandexWebAuthState> GetStateAsync(CancellationToken ct = default)
    {
        var state = _state;
        if (state != null)
            return state;

        await _lock.WaitAsync(ct);
        try
        {
            _state ??= await LoadStateAsync(ct);
            return _state;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RefreshAsync(bool force = false, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!force && DateTime.UtcNow - _lastRefreshUtc < _options.MinRefreshInterval)
            {
                // Недавно уже обновляли — просто перечитаем файл и ckey
                _state = null;
                return false;
            }

            _lastRefreshUtc = DateTime.UtcNow;
            _state = null;

            SeedCookieFileIfMissing();
            return await cookieRefresher.RefreshCookiesAsync(ct: ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _state = null;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    private async Task<YandexWebAuthState> LoadStateAsync(CancellationToken ct)
    {
        SeedCookieFileIfMissing();

        var cookiePath = Path.GetFullPath(_options.CookiePath);
        if (!File.Exists(cookiePath))
            throw new YandexWebAuthException(
                $"Файл cookies {cookiePath} не найден. Задайте YandexCalendar__Cookie или YandexCalendar__Login/Password.");

        var cookieHeader = (await File.ReadAllTextAsync(cookiePath, ct)).Trim();
        var cookies = ParseCookies(cookieHeader);

        if (!cookies.TryGetValue("yandexuid", out var uid) || string.IsNullOrEmpty(uid))
            throw new YandexWebAuthException("В cookies нет yandexuid");

        var ckey = await FetchCalendarCKeyAsync(cookieHeader, uid, ct);
        logger.LogInformation("🔑 Получен ckey Яндекс.Календаря (логин: {Login})",
            cookies.GetValueOrDefault("yandex_login", "н/д"));

        return new YandexWebAuthState(cookieHeader, uid, ckey);
    }

    private async Task<string> FetchCalendarCKeyAsync(string cookieHeader, string uid, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://calendar.yandex.ru/?uid={uid}");
        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await client.SendAsync(request, ct);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        if (location.Contains("passport.yandex", StringComparison.OrdinalIgnoreCase))
            throw new YandexWebAuthException("Cookies протухли: календарь редиректит на passport");

        var html = await response.Content.ReadAsStringAsync(ct);
        var match = CKeyRegex().Match(html);
        if (!match.Success)
            throw new YandexWebAuthException($"Не удалось найти ckey на странице календаря (HTTP {(int)response.StatusCode})");

        return match.Groups[1].Value;
    }

    private void SeedCookieFileIfMissing()
    {
        var cookiePath = Path.GetFullPath(_options.CookiePath);
        if (File.Exists(cookiePath) || string.IsNullOrWhiteSpace(_options.Cookie))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(cookiePath)!);
        File.WriteAllText(cookiePath, _options.Cookie.Trim() + Environment.NewLine);
        logger.LogInformation("📥 Cookies из конфигурации записаны в {CookiePath}", cookiePath);
    }

    private static Dictionary<string, string> ParseCookies(string cookieHeader)
    {
        var result = new Dictionary<string, string>();
        foreach (var item in cookieHeader.Split(';'))
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2)
                result[parts[0].Trim()] = parts[1].Trim();
        }

        return result;
    }

    [GeneratedRegex("\"ckey\"\\s*:\\s*\"([^\"]+)\"")]
    private static partial Regex CKeyRegex();
}

/// <summary>
///     Сессия Яндекса недействительна (нет cookies / протухли / нет ckey).
/// </summary>
public class YandexWebAuthException(string message) : Exception(message);
