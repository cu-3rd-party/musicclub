using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Отметка о попытке угадать логин Яндекса пользователя по его отображаемому имени
/// (одна строка на пользователя). Таблица <c>yandex_login_guess</c>.
/// </summary>
public class YandexLoginGuess
{
    public Guid UserId { get; set; }              // PK, FK -> app_user
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset AttemptedAt { get; set; }
    public string Query { get; set; } = string.Empty;   // Имя, по которому искали
    public YandexLoginGuessStatus Status { get; set; }
    public string? Email { get; set; }            // Найденный email (в т.ч. при конфликте)
    public string? Error { get; set; }
}
