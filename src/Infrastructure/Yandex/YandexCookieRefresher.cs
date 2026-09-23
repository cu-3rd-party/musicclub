using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Реализация сервиса обновления cookies Яндекс.Календаря через Playwright.
/// </summary>
public partial class YandexCookieRefresher : IYandexCookieRefresher
{
    private readonly string _cookiePath;
    private readonly string _profileDir;
    private readonly ILogger<YandexCookieRefresher> _logger;

    private const string TargetUrl = "https://calendar.yandex.ru/";
    private const string ProfileDirName = ".yandex_profile";

    public YandexCookieRefresher(
        ILogger<YandexCookieRefresher> logger,
        string? cookiePath = null,
        string? profileDir = null)
    {
        _logger = logger;
        _cookiePath = cookiePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cookie.txt");
        _profileDir = profileDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ProfileDirName);
    }

    public async Task<bool> RefreshCookiesAsync(bool headless = true, bool forceLogin = false, CancellationToken ct = default)
    {
        _logger.LogInformation("🔄 Запуск обновления cookies Яндекс... (headless={Headless}, forceLogin={ForceLogin})", 
            headless, forceLogin);

        var absProfileDir = Path.GetFullPath(_profileDir);
        var absCookiePath = Path.GetFullPath(_cookiePath);

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchPersistentContextAsync(absProfileDir, new()
        {
            Headless = headless,
            Args = new[] { "--disable-blink-features=AutomationControlled" },
            ViewportSize = new() { Width = 1280, Height = 800 },
        });

        try
        {

            var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();

            // Если есть существующий cookie.txt, загружаем cookies
            if (File.Exists(absCookiePath))
            {
                try
                {
                    var oldCookieStr = await File.ReadAllTextAsync(absCookiePath, ct);
                    var cookies = ParseCookieString(oldCookieStr);
                    await browser.AddCookiesAsync(cookies);
                    _logger.LogDebug("📥 Загружены существующие cookies");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Не удалось прочитать старый cookie.txt");
                }
            }

            _logger.LogInformation("🌐 Переход на {TargetUrl}...", TargetUrl);

            await page.GotoAsync(TargetUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            await page.WaitForTimeoutAsync(3000);

            // Проверяем, попали ли на страницу логина
            var currentUrl = page.Url;
            if (currentUrl.Contains("passport.yandex.ru") || forceLogin)
            {
                _logger.LogWarning("🔑 Требуется авторизация в Яндекс.");
                if (headless)
                {
                    _logger.LogInformation("💡 Открываем окно браузера для входа...");
                    await browser.CloseAsync();
                    // Перезапускаем в не-headless режиме
                    return await RefreshCookiesAsync(headless: false, forceLogin: true, ct);
                }
                else
                {
                    _logger.LogInformation("⏳ Пожалуйста, авторизуйтесь в открывшемся окне браузера...");
                    
                    // Ждем авторизации (максимум 3 минуты)
                    for (var i = 0; i < 36; i++)
                    {
                        await Task.Delay(5000, ct);
                        
                        if (!page.Url.Contains("passport.yandex.ru") && page.Url.Contains("calendar.yandex.ru"))
                        {
                            _logger.LogInformation("✅ Авторизация успешно выполнена!");
                            await page.WaitForTimeoutAsync(3000);
                            break;
                        }
                    }
                }
            }

            // Получаем обновленные cookies
            var allCookies = await browser.CookiesAsync();
            var yandexCookies = allCookies
                .Where(c => c.Domain.Contains("yandex"))
                .ToList();

            await browser.CloseAsync();

            var sessionId = yandexCookies.FirstOrDefault(c => c.Name == "Session_id")?.Value;
            var sessionId2 = yandexCookies.FirstOrDefault(c => c.Name == "sessionid2")?.Value;

            if (string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(sessionId2))
            {
                _logger.LogError("❌ Не удалось получить Session_id из cookies. Возможно, сессия неактивна.");
                return false;
            }

            // Собираем новую строку cookies
            var cookiePairs = yandexCookies.Select(c => $"{c.Name}={c.Value}");
            var newCookieStr = string.Join("; ", cookiePairs);

            // Записываем в cookie.txt
            await File.WriteAllTextAsync(absCookiePath, newCookieStr + Environment.NewLine, ct);

            var yandexLogin = yandexCookies.FirstOrDefault(c => c.Name == "yandex_login")?.Value ?? "н/д";
            _logger.LogInformation(
                "✅ Файл {CookiePath} успешно обновлен! (Получено {Count} cookies, логин: {Login})", 
                _cookiePath, yandexCookies.Count, yandexLogin);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при обновлении cookies через Playwright: {Message}", ex.Message);
            return false;
        }
        finally
        {
            await browser.CloseAsync();
            playwright.Dispose();
        }
    }

    /// <summary>
    /// Парсит строку cookies в формат Playwright.
    /// </summary>
    private static List<Cookie> ParseCookieString(string cookieStr)
    {
        var cookies = new List<Cookie>();
        
        foreach (var item in cookieStr.Split(';'))
        {
            var parts = item.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            cookies.Add(new Cookie
            {
                Name = key,
                Value = value,
                Domain = ".yandex.ru",
                Path = "/",
            });
        }

        return cookies;
    }
}
