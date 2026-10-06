using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Application.Services.User;

public class YandexEmailBackfillService(
    IApplicationUserRepository userRepository,
    IYandexLoginGuessRepository guessRepository,
    IYandexEmailSearchService emailSearchService,
    IUnitOfWork unitOfWork,
    ILogger<YandexEmailBackfillService> logger) : IYandexEmailBackfillService
{
    private const string CorporateDomain = "@edu.centraluniversity.ru";
    private const int MaxConsecutiveErrors = 3;
    private static readonly TimeSpan DelayBetweenUsers = TimeSpan.FromSeconds(1);

    public async Task<YandexLoginGuessStatus> GuessLoginAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var query = user.DisplayName.Trim();
        string? email = null;
        string? error = null;
        YandexLoginGuessStatus status;

        try
        {
            foreach (var candidate in BuildQueries(query))
            {
                email = await emailSearchService.SearchEmailByNameAsync(candidate, ct);
                if (!string.IsNullOrEmpty(email))
                    break;
            }

            status = await ApplyEmailAsync(user, email, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Yandex login guess failed for user {UserId} ('{Query}')", user.Id, query);
            status = YandexLoginGuessStatus.Error;
            error = ex.Message;
        }

        var guess = await guessRepository.FindByUserIdAsync(user.Id, ct);
        if (guess == null)
        {
            guess = new YandexLoginGuess { UserId = user.Id };
            await guessRepository.AddAsync(guess, ct);
        }

        guess.AttemptedAt = DateTimeOffset.UtcNow;
        guess.Query = query;
        guess.Status = status;
        guess.Email = email;
        guess.Error = error;

        await unitOfWork.SaveChangesAsync(ct);
        return status;
    }

    public async Task<YandexLoginBackfillResult> BackfillAllUsersAsync(CancellationToken ct = default)
    {
        var users = await guessRepository.GetUsersToGuessAsync(ct);
        int found = 0, notFound = 0, conflicts = 0, errors = 0, consecutiveErrors = 0;

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();

            var status = await GuessLoginAsync(user, ct);
            switch (status)
            {
                case YandexLoginGuessStatus.Found:
                    found++;
                    break;
                case YandexLoginGuessStatus.NotFound:
                    notFound++;
                    break;
                case YandexLoginGuessStatus.Conflict:
                    conflicts++;
                    break;
                case YandexLoginGuessStatus.Error:
                    errors++;
                    break;
            }

            consecutiveErrors = status == YandexLoginGuessStatus.Error ? consecutiveErrors + 1 : 0;
            if (consecutiveErrors >= MaxConsecutiveErrors)
            {
                logger.LogWarning("Yandex login backfill stopped after {Count} consecutive errors", consecutiveErrors);
                break;
            }

            await Task.Delay(DelayBetweenUsers, ct);
        }

        return new YandexLoginBackfillResult(found, notFound, conflicts, errors);
    }

    private async Task<YandexLoginGuessStatus> ApplyEmailAsync(ApplicationUser user,
        string? email,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(email))
            return YandexLoginGuessStatus.NotFound;

        var normalized = email.Trim().ToLowerInvariant();
        if (!normalized.EndsWith(CorporateDomain, StringComparison.Ordinal))
        {
            logger.LogInformation("Yandex login guess for user {UserId}: '{Email}' is not a corporate email",
                user.Id,
                email);
            return YandexLoginGuessStatus.NotFound;
        }

        var login = normalized[..^CorporateDomain.Length];
        if (login.Length == 0)
            return YandexLoginGuessStatus.NotFound;

        var owner = await userRepository.FindByYandexLoginAsync(login, ct);
        if (owner != null && owner.Id != user.Id)
        {
            logger.LogWarning("Yandex login guess for user {UserId}: '{Login}' already belongs to user {OwnerId}",
                user.Id,
                login,
                owner.Id);
            return YandexLoginGuessStatus.Conflict;
        }

        user.YandexLogin = login;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        userRepository.Update(user);

        logger.LogInformation("Guessed YandexLogin for user {UserId} ('{DisplayName}'): {Login}",
            user.Id,
            user.DisplayName,
            login);
        return YandexLoginGuessStatus.Found;
    }

    /// <summary>
    /// Поиск ждёт «Фамилия Имя», а в Telegram обычно «Имя Фамилия» — для двух слов пробуем оба порядка.
    /// </summary>
    private static IEnumerable<string> BuildQueries(string displayName)
    {
        yield return displayName;

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
            yield return $"{parts[1]} {parts[0]}";
    }
}
