using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.DTOs.Calendar;

/// <summary>
/// DTO для представления события календаря.
/// </summary>
public class CalendarEventDto
{
    /// <summary>
    /// Идентификатор события.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор пользователя.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Заголовок события.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание события.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Начало события.
    /// </summary>
    public DateTimeOffset StartAt { get; set; }

    /// <summary>
    /// Конец события.
    /// </summary>
    public DateTimeOffset EndAt { get; set; }

    /// <summary>
    /// Местоположение события.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Тип события.
    /// </summary>
    public CalendarEventType EventType { get; set; }

    /// <summary>
    /// Тип источника события (например, "tracklist", "manual").
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор источника события.
    /// </summary>
    public Guid? SourceId { get; set; }
}
