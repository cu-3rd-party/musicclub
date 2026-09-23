using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Сервис для обновления cookies Яндекс.Календаря через Playwright.
/// Перенос функционала из cookie_refresher.py.
/// </summary>
public interface IYandexCookieRefresher
{
    /// <summary>
    /// Обновляет cookies Яндекс.Календаря.
    /// </summary>
    /// <param name="headless">Если true — браузер запускается без GUI.</param>
    /// <param name="forceLogin">Если true — принудительно показывает окно логина.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>True если cookies успешно обновлены.</returns>
    Task<bool> RefreshCookiesAsync(bool headless = true, bool forceLogin = false, CancellationToken ct = default);
}
