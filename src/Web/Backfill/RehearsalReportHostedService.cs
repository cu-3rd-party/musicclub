using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Calendar;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Web.Backfill;

/// <summary>
/// Рассылка отчётов о репетициях: 1-го числа в 10:00 МСК, плюс при старте и раз в час —
/// чтобы дослать пропущенное (простой, недоступный получатель, ошибка Яндекса).
/// </summary>
public sealed class RehearsalReportHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RehearsalReportOptions> options,
    ILogger<RehearsalReportHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Отчёты о репетициях выключены (RehearsalReports:Enabled)");
            return;
        }

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
                logger.LogError(ex, "Rehearsal report run failed");
            }

            var now = DateTimeOffset.UtcNow;
            var untilSend = RehearsalReportService.NextSendTime(now) - now;
            var delay = untilSend < RetryInterval ? untilSend : RetryInterval;

            try { await Task.Delay(delay, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRehearsalReportService>();
        await service.ProcessDueReportsAsync(DateTimeOffset.UtcNow, cancellationToken);
    }
}
