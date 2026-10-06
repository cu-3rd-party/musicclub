using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;

namespace CuMusicClub.Web.BackgroundServices;

public class ExpiredAuthLinkCleanupService(
    IServiceProvider serviceProvider,
    ILogger<ExpiredAuthLinkCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanupExpiredLinks(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error cleaning up expired auth links");
                }
            }
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task CleanupExpiredLinks(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var authLinkRepository = scope.ServiceProvider.GetRequiredService<ITgAuthLinkRepository>();

        var cutoffDate = DateTimeOffset.UtcNow.Subtract(TelegramAuthService.AuthLinkLifetime);
        var expiredLinks = await authLinkRepository.Query()
            .Where(l => l.Created < cutoffDate)
            .ToListAsync(cancellationToken);

        foreach (var link in expiredLinks)
        {
            authLinkRepository.Remove(link);
        }

        if (expiredLinks.Count > 0)
        {
            await authLinkRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Cleaned up {Count} expired auth links", expiredLinks.Count);
        }
    }
}
