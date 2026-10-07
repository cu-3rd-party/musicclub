using CuMusicClub.Application.Services.User;

namespace CuMusicClub.Web.Backfill;

/// <summary>
/// Каждые 10 минут угадывает логины Яндекса по фамилии и имени (или отображаемому имени) для
/// пользователей, у которых логина нет. Каждая попытка фиксируется в <c>yandex_login_guess</c>, поэтому
/// пользователь перебирается повторно только при ошибке или если его имя изменилось.
/// </summary>
public sealed class YandexLoginBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<YandexLoginBackfillHostedService> logger) : BackgroundService
{
    // Даём приложению подняться и Playwright обновить cookies
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try { await Task.Delay(StartupDelay, cancellationToken); }
        catch (OperationCanceledException) { return; }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Yandex login backfill failed");
            }

            try { await Task.Delay(Interval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var search = scope.ServiceProvider.GetRequiredService<IYandexEmailSearchService>();
        if (!search.IsAvailable)
        {
            logger.LogDebug("Yandex login backfill: Yandex web calendar is not configured, skipping");
            return;
        }

        var backfill = scope.ServiceProvider.GetRequiredService<IYandexEmailBackfillService>();
        var result = await backfill.BackfillAllUsersAsync(cancellationToken);
        if (result.Found + result.NotFound + result.Conflicts + result.Errors == 0)
            return;

        logger.LogInformation(
            "Yandex login backfill finished. Found: {Found}, not found: {NotFound}, conflicts: {Conflicts}, errors: {Errors}",
            result.Found,
            result.NotFound,
            result.Conflicts,
            result.Errors);
    }
}
