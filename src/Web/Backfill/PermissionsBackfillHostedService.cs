using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Web.Backfill;

public sealed class PermissionsBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PermissionsBackfillHostedService> logger) : IHostedService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunBatchAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Permissions backfill run failed");
            }

            try
            {
                await Task.Delay(Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunBatchAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Starting permissions backfill run");

        var succeeded = 0;
        var skipped = 0;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var usersWithoutPermissions = await db
                .Users
                .Where(u => !db
                    .UserClaims
                    .Any(c => c.UserId == u.Id && c.ClaimType == PermissionClaimTypes.Permission))
                .OrderBy(u => u.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (usersWithoutPermissions.Count == 0) break;

            logger.LogDebug("Processing batch of {Count} users without permissions", usersWithoutPermissions.Count);

            foreach (var user in usersWithoutPermissions)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    await permissionService.GrantDefaultAsync(user, cancellationToken);
                    succeeded++;
                    logger.LogDebug("Granted default permissions to user {UserId} ({UserName})", user.Id, user.UserName);
                }
                catch (Exception ex)
                {
                    skipped++;
                    logger.LogError(ex,
                        "Failed to grant default permissions to user {UserId} ({UserName})",
                        user.Id,
                        user.UserName);
                }
            }
        }

        if (succeeded != 0)
            logger.LogInformation("Permissions backfill run finished. Granted: {Succeeded}, Failed: {Skipped}",
                succeeded,
                skipped);
        else
            logger.LogDebug("Permissions backfill run finished. Granted: {Succeeded}, Failed: {Skipped}",
                succeeded,
                skipped);
    }
}
