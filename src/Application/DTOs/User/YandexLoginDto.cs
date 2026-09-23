namespace CuMusicClub.Application.DTOs.User;

/// <summary>
/// DTO для отображения YandexLogin пользователя.
/// </summary>
public class YandexLoginDto
{
    public Guid UserId { get; set; }
    public string? YandexLogin { get; set; }
    public bool HasYandexLogin => !string.IsNullOrWhiteSpace(YandexLogin);
}
