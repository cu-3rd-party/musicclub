using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

public interface ICalendarEventRepository : IRepository<CalendarEvent>
{
    /// <summary>
    /// Получить события пользователя за период
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetUserEventsAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default);
    
    /// <summary>
    /// Получить активные (неудалённые) события пользователя за период
    /// </summary>
    Task<IEnumerable<CalendarEvent>> GetUserActiveEventsAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default);
    
    /// <summary>
    /// Найти событие по источнику
    /// </summary>
    Task<CalendarEvent?> FindBySourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default);
    
    /// <summary>
    /// Найти события пользователя по источнику
    /// </summary>
    Task<IEnumerable<CalendarEvent>> FindBySourceAndUserAsync(string sourceType, Guid sourceId, Guid userId, CancellationToken ct = default);
}
