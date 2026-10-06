using CuMusicClub.Application.Services.Calendar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Web.Backfill;

/// <summary>
/// Догоняет Яндекс.Календарь бронированиями, созданными только в боте:
/// пока интеграция была выключена или Яндекс не отвечал. Запускается при старте и затем периодически.
/// Прошлые репетиции добавляются без участников, чтобы не рассылать приглашения задним числом.
/// </summary>
public class CalendarSyncBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ICalendarIntegration integration,
    ILogger<CalendarSyncBackfillHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!integration.IsSyncEnabled)
        {
            logger.LogInformation("Синхронизация с календарём выключена — бронирования хранятся только в боте");
            return;
        }

        // Даём приложению подняться и Playwright обновить cookies
        await Task.Delay(StartupDelay, stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var calendarSync = scope.ServiceProvider.GetRequiredService<ICalendarSyncService>();

            var result = await calendarSync.BackfillMissingEventsAsync(stoppingToken);

            if (result.Created > 0 || result.Deleted > 0 || result.Failed > 0)
                logger.LogInformation(
                    "✅ Backfill календаря: создано {Created}, удалено {Deleted}, ошибок {Failed}",
                    result.Created, result.Deleted, result.Failed);
            else
                logger.LogDebug("Backfill календаря: всё синхронизировано");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "❌ Ошибка backfill календаря");
        }
    }
}
