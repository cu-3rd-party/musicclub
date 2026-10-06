using CuMusicClub.Application.Services.Calendar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Web.Backfill;

/// <summary>
/// Фоновая служба для синхронизации бронирований с CalDAV при старте приложения.
/// </summary>
public class CalendarSyncBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<CalendarSyncBackfillHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("🔄 Запуск синхронизации календаря...");

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var calDavSync = scope.ServiceProvider.GetRequiredService<ICalendarSyncService>();

            var createdCount = await calDavSync.BackfillMissingEventsAsync(stoppingToken);

            if (createdCount > 0)
                logger.LogInformation("✅ Backfilled {Count} missing CalDAV events", createdCount);
            else
                logger.LogInformation("✅ All CalDAV events are in sync");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during calendar sync backfill");
        }
    }
}
