using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

public interface ICalendarFeedRepository : IRepository<CalendarFeed>
{
    /// <summary>
    /// Получить фид пользователя
    /// </summary>
    Task<CalendarFeed?> GetUserFeedAsync(Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Получить фид по токену
    /// </summary>
    Task<CalendarFeed?> GetByTokenAsync(string token, CancellationToken ct = default);
    
    /// <summary>
    /// Создать или обновить фид пользователя
    /// </summary>
    Task<CalendarFeed> UpsertFeedAsync(CalendarFeed feed, CancellationToken ct = default);
}
