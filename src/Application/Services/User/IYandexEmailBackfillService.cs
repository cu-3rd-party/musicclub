namespace CuMusicClub.Application.Services.User;

/// <summary>
/// Backfills YandexLogin for users by searching Yandex Calendar for their emails.
/// </summary>
public interface IYandexEmailBackfillService
{
    /// <summary>
    /// Searches for and updates a user's YandexLogin if not already set.
    /// Queries Yandex Calendar's suggest-contacts by surname/name.
    /// </summary>
    Task<bool> BackfillUserEmailAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Backfills emails for all users without YandexLogin (up to limit).
    /// </summary>
    Task<int> BackfillAllUsersAsync(int limit = 50, CancellationToken ct = default);
}
