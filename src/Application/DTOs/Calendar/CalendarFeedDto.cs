namespace CuMusicClub.Application.DTOs.Calendar;

/// <summary>
/// DTO для представления ICS-фида пользователя.
/// </summary>
public class CalendarFeedDto
{
    /// <summary>
    /// Идентификатор пользователя.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Токен доступа к фиду.
    /// </summary>
    public string FeedToken { get; set; } = string.Empty;

    /// <summary>
    /// Полный URL для подписки на календарь.
    /// </summary>
    public string IcsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Признак активного фида.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Дата создания фида.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата отзыва фида (null если активен).
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
