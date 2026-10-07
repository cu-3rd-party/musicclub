using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.Services.User;

/// <summary>Итог прогона бекфилла логинов Яндекса.</summary>
public sealed record YandexLoginBackfillResult(int Found, int NotFound, int Conflicts, int Errors);

/// <summary>
/// Угадывает YandexLogin пользователей по фамилии и имени (или отображаемому имени) через suggest-contacts Яндекс.Календаря
/// и отмечает каждую попытку в <c>yandex_login_guess</c>.
/// </summary>
public interface IYandexEmailBackfillService
{
    /// <summary>
    /// Пытается угадать логин пользователя, записывает его при однозначном совпадении
    /// и сохраняет результат попытки. Изменения сохраняются сразу.
    /// </summary>
    Task<YandexLoginGuessStatus> GuessLoginAsync(ApplicationUser user, CancellationToken ct = default);

    /// <summary>
    /// Прогоняет всех пользователей без логина, для которых ещё не было завершённой попытки.
    /// Останавливается, если Яндекс несколько раз подряд отвечает ошибкой.
    /// </summary>
    Task<YandexLoginBackfillResult> BackfillAllUsersAsync(CancellationToken ct = default);
}
