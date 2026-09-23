namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Персональный ICS-фид пользователя для подписки в календаре
/// </summary>
public class CalendarFeed
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID пользователя
    /// </summary>
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    
    /// <summary>
    /// Уникальный токен для доступа к фиду (в URL)
    /// </summary>
    public string FeedToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Фид активен (можно ли скачивать ICS)
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Дата отзыва токена (если отозван)
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}
