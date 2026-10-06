using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Реализация сервиса обновления cookies Яндекс.Календаря через Playwright.
///     Держит persistent-профиль Chromium: пока профиль жив, Яндекс сам продлевает Session_id при заходе на календарь.
///     Если сессия всё же протухла — пробует войти по логину/паролю из <see cref="YandexWebCalendarOptions" />.
/// </summary>
public class YandexCookieRefresher(
    IOptions<YandexWebCalendarOptions> options,
    ILogger<YandexCookieRefresher> logger) : IYandexCookieRefresher
{
    private const string TargetUrl = "https://calendar.yandex.ru/";
    private const string PassportHost = "passport.yandex.ru";
    private static readonly TimeSpan LoginWaitTimeout = TimeSpan.FromMinutes(3);

    private readonly YandexWebCalendarOptions _options = options.Value;

    public async Task<bool> RefreshCookiesAsync(bool headless = true, bool forceLogin = false,
        CancellationToken ct = default)
    {
        logger.LogInformation("🔄 Запуск обновления cookies Яндекс... (headless={Headless}, forceLogin={ForceLogin})",
            headless, forceLogin);

        var absProfileDir = Path.GetFullPath(_options.ProfileDir);
        var absCookiePath = Path.GetFullPath(_options.CookiePath);
        Directory.CreateDirectory(absProfileDir);
        Directory.CreateDirectory(Path.GetDirectoryName(absCookiePath)!);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(absProfileDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = headless,
                Args = ["--disable-blink-features=AutomationControlled", "--no-sandbox", "--disable-dev-shm-usage"],
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                Locale = "ru-RU",
                TimezoneId = _options.TimeZone
            });

        try
        {
            var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();

            // Подкладываем cookies из файла: так можно «засеять» профиль строкой из DevTools
            if (File.Exists(absCookiePath))
                try
                {
                    var oldCookieStr = await File.ReadAllTextAsync(absCookiePath, ct);
                    await browser.AddCookiesAsync(ParseCookieString(oldCookieStr));
                    logger.LogDebug("📥 Загружены существующие cookies");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "⚠️ Не удалось прочитать старый {CookiePath}", absCookiePath);
                }

            logger.LogInformation("🌐 Переход на {TargetUrl}...", TargetUrl);
            await page.GotoAsync(TargetUrl,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            await page.WaitForTimeoutAsync(3000);

            if (IsOnPassport(page) || forceLogin)
            {
                logger.LogWarning("🔑 Требуется авторизация в Яндекс.");

                var loggedIn = await TryAutoLoginAsync(page, ct);
                if (!loggedIn)
                {
                    if (headless && _options.AllowInteractiveLogin)
                    {
                        logger.LogInformation("💡 Открываем окно браузера для ручного входа...");
                        await browser.CloseAsync();
                        return await RefreshCookiesAsync(false, true, ct);
                    }

                    if (!headless)
                        loggedIn = await WaitForManualLoginAsync(page, ct);
                }

                if (!loggedIn)
                {
                    logger.LogError(
                        "❌ Не удалось авторизоваться в Яндекс. Задайте YandexCalendar__Login/Password, " +
                        "свежие cookies в {CookiePath} или войдите вручную (AllowInteractiveLogin=true локально)",
                        absCookiePath);
                    return false;
                }
            }

            var yandexCookies = (await browser.CookiesAsync())
                .Where(c => c.Domain.Contains("yandex"))
                .ToList();

            var sessionId = yandexCookies.FirstOrDefault(c => c.Name == "Session_id")?.Value;
            var sessionId2 = yandexCookies.FirstOrDefault(c => c.Name == "sessionid2")?.Value;

            if (string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(sessionId2))
            {
                logger.LogError("❌ Не удалось получить Session_id из cookies. Возможно, сессия неактивна.");
                return false;
            }

            // Одинаковые имена на разных поддоменах схлопываем: берём cookie с самым «общим» доменом
            var cookiePairs = yandexCookies
                .GroupBy(c => c.Name)
                .Select(g => g.OrderBy(c => c.Domain.Length).First())
                .Select(c => $"{c.Name}={c.Value}");
            var newCookieStr = string.Join("; ", cookiePairs);

            // Пишем атомарно, чтобы читатели не увидели полуфайл
            var tmpPath = absCookiePath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, newCookieStr + Environment.NewLine, ct);
            File.Move(tmpPath, absCookiePath, true);

            var yandexLogin = yandexCookies.FirstOrDefault(c => c.Name == "yandex_login")?.Value ?? "н/д";
            logger.LogInformation(
                "✅ Файл {CookiePath} успешно обновлен! (Получено {Count} cookies, логин: {Login})",
                absCookiePath, yandexCookies.Count, yandexLogin);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "❌ Ошибка при обновлении cookies через Playwright: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Автологин на passport.yandex.ru: логин → «Войти» → пароль → «Войти».
    ///     Не пройдёт, если Яндекс попросит капчу/SMS/push — тогда нужен ручной вход.
    /// </summary>
    private async Task<bool> TryAutoLoginAsync(IPage page, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Login) || string.IsNullOrWhiteSpace(_options.Password))
            return false;

        logger.LogInformation("🤖 Пробуем автоматический вход как {Login}...", _options.Login);

        try
        {
            if (!IsOnPassport(page))
                await page.GotoAsync($"https://{PassportHost}/auth?retpath={Uri.EscapeDataString(TargetUrl)}",
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });

            var loginInput = page.Locator("input[name='login']");
            if (await loginInput.IsVisibleAsync())
            {
                await loginInput.FillAsync(_options.Login);
                await page.Locator("button[type='submit']").First.ClickAsync();
            }

            var passwordInput = page.Locator("input[name='passwd']");
            await passwordInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 20000 });
            await passwordInput.FillAsync(_options.Password);
            await page.Locator("button[type='submit']").First.ClickAsync();

            await page.WaitForURLAsync(url => url.Contains("calendar.yandex.ru") && !url.Contains(PassportHost),
                new PageWaitForURLOptions { Timeout = 30000 });
            await page.WaitForTimeoutAsync(3000);

            logger.LogInformation("✅ Автоматический вход выполнен");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "⚠️ Автоматический вход не удался (капча / подтверждение / смена вёрстки). Текущий URL: {Url}",
                page.Url);
            return false;
        }
    }

    private async Task<bool> WaitForManualLoginAsync(IPage page, CancellationToken ct)
    {
        logger.LogInformation("⏳ Пожалуйста, авторизуйтесь в открывшемся окне браузера...");

        var deadline = DateTime.UtcNow + LoginWaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(5000, ct);

            if (!IsOnPassport(page) && page.Url.Contains("calendar.yandex.ru"))
            {
                logger.LogInformation("✅ Авторизация успешно выполнена!");
                await page.WaitForTimeoutAsync(3000);
                return true;
            }
        }

        return false;
    }

    private static bool IsOnPassport(IPage page)
    {
        return page.Url.Contains(PassportHost);
    }

    /// <summary>
    ///     Парсит строку cookies в формат Playwright.
    /// </summary>
    private static List<Cookie> ParseCookieString(string cookieStr)
    {
        var cookies = new List<Cookie>();

        foreach (var item in cookieStr.Split(';'))
        {
            var parts = item.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0])) continue;

            cookies.Add(new Cookie
            {
                Name = parts[0].Trim(),
                Value = parts[1].Trim(),
                Domain = ".yandex.ru",
                Path = "/"
            });
        }

        return cookies;
    }
}
