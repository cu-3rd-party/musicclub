using CuMusicClub.Application.Services.User;

namespace CuMusicClub.Web.Backfill;

/// <summary>
/// Один раз при старте угадывает логины Яндекса по отображаемому имени для пользователей,
/// у которых логина нет. Каждая попытка фиксируется в <c>yandex_login_guess</c>, поэтому
/// пользователь перебирается только один раз (кроме попыток, упавших с ошибкой).
/// </summary>
public sealed class YandexLoginBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<YandexLoginBackfillHostedService> logger) : BackgroundService
{
    // Даём приложению подняться и Playwright обновить cookies
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(StartupDelay, cancellationToken);
            await RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Yandex login backfill failed");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var search = scope.ServiceProvider.GetRequiredService<IYandexEmailSearchService>();
        if (!search.IsAvailable)
        {
            logger.LogInformation("Yandex login backfill: Yandex web calendar is not configured, skipping");
            return;
        }

        var backfill = scope.ServiceProvider.GetRequiredService<IYandexEmailBackfillService>();
        var result = await backfill.BackfillAllUsersAsync(cancellationToken);

        logger.LogInformation(
            "Yandex login backfill finished. Found: {Found}, not found: {NotFound}, conflicts: {Conflicts}, errors: {Errors}",
            result.Found,
            result.NotFound,
            result.Conflicts,
            result.Errors);
    }
}
