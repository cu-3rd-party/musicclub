using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

/// <summary>Репозиторий попыток угадать логин Яндекса по имени пользователя.</summary>
public interface IYandexLoginGuessRepository : IRepository<YandexLoginGuess>
{
    /// <summary>Попытка для пользователя (одна на пользователя).</summary>
    Task<YandexLoginGuess?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Пользователи без логина Яндекса, с непустым именем и без завершённой попытки угадать логин
    /// по текущему имени (см. <see cref="ApplicationUser.GetFullName"/>)
    /// (попытки со статусом <see cref="Enums.YandexLoginGuessStatus.Error"/> повторяются).
    /// </summary>
    Task<IReadOnlyList<ApplicationUser>> GetUsersToGuessAsync(CancellationToken ct = default);
}
