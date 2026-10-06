using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Yandex;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Application.Services.User;

public class YandexEmailBackfillService(
    IApplicationUserRepository userRepository,
    IYandexEmailSearchService emailSearchService,
    IUnitOfWork unitOfWork,
    ILogger<YandexEmailBackfillService> logger) : IYandexEmailBackfillService
{
    public async Task<bool> BackfillUserEmailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found", userId);
            return false;
        }

        // Already has email
        if (!string.IsNullOrEmpty(user.YandexLogin))
            return false;

        // Can't search without name
        if (string.IsNullOrEmpty(user.DisplayName))
        {
            logger.LogDebug("User {UserId} has no display name, skipping", userId);
            return false;
        }

        var email = await emailSearchService.SearchEmailByNameAsync(user.DisplayName, ct);
        if (string.IsNullOrEmpty(email))
            return false;

        // Extract login from email (remove @edu.centraluniversity.ru)
        var login = email.Replace("@edu.centraluniversity.ru", string.Empty);
        if (string.IsNullOrEmpty(login))
            return false;

        user.YandexLogin = login;
        userRepository.Update(user);

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Backfilled YandexLogin for user {UserId}: {Login}", userId, login);

        return true;
    }

    public async Task<int> BackfillAllUsersAsync(int limit = 50, CancellationToken ct = default)
    {
        // Get users without YandexLogin
        var users = await userRepository.GetUsersWithoutPermissionsAsync(limit, ct);
        var usersWithoutEmail = users
            .Where(u => string.IsNullOrEmpty(u.YandexLogin) && !string.IsNullOrEmpty(u.DisplayName))
            .ToList();

        if (usersWithoutEmail.Count == 0)
            return 0;

        var updated = 0;
        foreach (var user in usersWithoutEmail)
        {
            try
            {
                if (await BackfillUserEmailAsync(user.Id, ct))
                    updated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error backfilling email for user {UserId}", user.Id);
            }
        }

        return updated;
    }
}
