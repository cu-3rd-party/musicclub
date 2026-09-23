using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

/// <summary>Репозиторий пользователей приложения.</summary>
public interface IApplicationUserRepository : IRepository<ApplicationUser>
{
    Task<ApplicationUser?> FindByTgUserIdAsync(long tgUserId, CancellationToken cancellationToken = default);

    /// <summary>Пользователи, у которых ещё нет ни одного права (для бэкфилла прав).</summary>
    Task<IReadOnlyList<ApplicationUser>> GetUsersWithoutPermissionsAsync(int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Все права пользователя (строковые значения из таблицы <c>user_permissions</c>).</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет права пользователю (идемпотентно — уже существующие не трогает).
    /// Не сохраняет изменения; вызов <see cref="IRepository{TEntity}.SaveChangesAsync"/> — обязанность вызывающего.
    /// </summary>
    Task GrantPermissionsAsync(Guid userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>Все пользователи (с настройками приватности), опционально с фильтром по имени/username.</summary>
    Task<IReadOnlyList<ApplicationUser>> GetUsersAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>Пользователи, у которых в <c>user_permissions</c> есть ровно это право.</summary>
    Task<IReadOnlyList<ApplicationUser>> GetUsersByPermissionAsync(string permission,
        CancellationToken cancellationToken = default);

    /// <summary>Настройки приватности пользователя (null — настроек нет, трактуется как «разрешено»).</summary>
    Task<UserPreferences?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сохраняет настройки приватности пользователя (upsert: создаёт строку при отсутствии).
    /// Не сохраняет изменения; вызов <see cref="IRepository{TEntity}.SaveChangesAsync"/> — обязанность вызывающего.
    /// </summary>
    Task SetPreferencesAsync(Guid userId,
        bool allowAdding,
        bool allowRemoving,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Найти пользователя по YandexLogin.
    /// </summary>
    Task<ApplicationUser?> FindByYandexLoginAsync(string yandexLogin, CancellationToken cancellationToken = default);
}