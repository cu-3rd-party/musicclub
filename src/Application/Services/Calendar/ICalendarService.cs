using CuMusicClub.Application.DTOs.Calendar;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сервис для управления персональными календарями пользователей.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Получить или создать фид для пользователя.
    /// </summary>
    Task<CalendarFeedDto> GetOrCreateFeedAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Перевыпустить токен фида.
    /// </summary>
    Task<CalendarFeedDto> RegenerateTokenAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Отозвать фид.
    /// </summary>
    Task<CalendarFeedDto> RevokeFeedAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Получить фид по токену.
    /// </summary>
    Task<CalendarFeedDto?> GetFeedByTokenAsync(string token, CancellationToken ct);

    /// <summary>
    /// Получить список событий пользователя за период.
    /// </summary>
    Task<List<CalendarEventDto>> GetEventsAsync(Guid userId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);

    /// <summary>
    /// Создать событие календаря.
    /// </summary>
    Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventRequest request, CancellationToken ct);

    /// <summary>
    /// Удалить событие.
    /// </summary>
    Task DeleteEventAsync(Guid eventId, CancellationToken ct);

    /// <summary>
    /// Синхронизировать событие из треклиста.
    /// </summary>
    Task SyncEventFromTracklistAsync(Guid trackItemId, CancellationToken ct);
}
