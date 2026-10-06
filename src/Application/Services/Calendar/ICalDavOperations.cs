namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Абстракция операций с CalDAV-календарём.
/// Позволяет Application слою зависеть от абстракции, а не от Infrastructure.
/// </summary>
public interface ICalDavOperations
{
    /// <summary>
    /// Получить список календарей.
    /// </summary>
    Task<List<CalDavCalendarInfo>> GetCalendarsAsync(CancellationToken ct = default);

    /// <summary>
    /// Создать календарь.
    /// </summary>
    Task<CalDavCalendarInfo> CreateCalendarAsync(string displayName, string? color = null, CancellationToken ct = default);

    /// <summary>
    /// Создать событие в календаре.
    /// </summary>
    Task<CalDavEventInfo> CreateEventAsync(string calendarUrl, CalDavEventInfo eventInfo, CancellationToken ct = default);

    /// <summary>
    /// Обновить событие.
    /// </summary>
    Task<CalDavEventInfo> UpdateEventAsync(CalDavEventInfo eventInfo, CancellationToken ct = default);

    /// <summary>
    /// Удалить событие.
    /// </summary>
    Task DeleteEventAsync(string eventUrl, CancellationToken ct = default);

    /// <summary>
    /// Получить событие.
    /// </summary>
    Task<CalDavEventInfo?> GetEventAsync(string eventUrl, CancellationToken ct = default);

    /// <summary>
    /// Получить события в диапазоне дат.
    /// </summary>
    Task<List<CalDavEventInfo>> GetEventsAsync(string calendarUrl, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// Проверить занятость в интервале.
    /// </summary>
    /// <param name="calendarUrl">URL календаря или email пользователя (если реализация умеет смотреть чужую занятость).</param>
    Task<bool> IsUserBusyAsync(string calendarUrl, DateTime start, DateTime end, CancellationToken ct = default);
}

/// <summary>
/// Информация о календаре.
/// </summary>
public record CalDavCalendarInfo(string Url, string? DisplayName, string? Color, string? ETag);

/// <summary>
/// Информация о событии.
/// </summary>
public record CalDavEventInfo(
    string Uid,
    string Title,
    string? Description,
    DateTime Start,
    DateTime End,
    bool IsAllDay,
    string? Location,
    string? ETag,
    string? Url,
    DateTime Created,
    IReadOnlyList<CalDavParticipant> Participants);

/// <summary>
/// Участник события.
/// </summary>
public record CalDavParticipant(
    string Email,
    string? Name,
    CalDavParticipantRole Role,
    CalDavParticipantStatus Status);

public enum CalDavParticipantRole
{
    Required,
    Optional,
    Chair
}

public enum CalDavParticipantStatus
{
    NeedsAction,
    Accepted,
    Declined,
    Tentative
}
