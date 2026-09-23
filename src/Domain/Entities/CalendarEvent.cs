using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Событие календаря пользователя
/// </summary>
public class CalendarEvent
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID пользователя, чьё это событие
    /// </summary>
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    
    /// <summary>
    /// Заголовок события
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание события
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Начало события
    /// </summary>
    public DateTime StartAt { get; set; }
    
    /// <summary>
    /// Конец события
    /// </summary>
    public DateTime EndAt { get; set; }
    
    /// <summary>
    /// Местоположение (адрес, название места)
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Тип источника события: "tracklist", "rehearsal", "personal"
    /// </summary>
    public string SourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// ID источника (GUID на EventTrackItem, Event или др.)
    /// </summary>
    public Guid? SourceId { get; set; }
    
    /// <summary>
    /// Тип события (репетиция, выступление, личное)
    /// </summary>
    public CalendarEventType EventType { get; set; }
    
    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Дата удаления (soft delete)
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
