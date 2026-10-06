using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Периодически прогоняет Playwright по calendar.yandex.ru, чтобы Яндекс продлевал Session_id,
///     а cookie-файл всегда был свежим — без ручного копирования cookies из браузера.
/// </summary>
public sealed class YandexCookieRefreshHostedService(
    IYandexWebSession session,
    IOptions<YandexWebCalendarOptions> options,
    ILogger<YandexCookieRefreshHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.RefreshInterval;
        if (interval <= TimeSpan.Zero || !session.IsConfigured)
        {
            logger.LogInformation("Фоновое обновление cookies Яндекса отключено (нет интервала или cookies/логина)");
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                var ok = await session.RefreshAsync(true, stoppingToken);
                if (!ok)
                    logger.LogWarning("⚠️ Плановое обновление cookies Яндекса не удалось — интеграция может отвалиться");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "❌ Ошибка планового обновления cookies Яндекса");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
