using CuMusicClub.Application.DTOs.User;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.User;

/// <summary>
/// Сервис для управления YandexLogin пользователей.
/// </summary>
public interface IYandexLoginService
{
    /// <summary>
    /// Получить YandexLogin пользователя.
    /// </summary>
    Task<YandexLoginDto?> GetYandexLoginAsync(Guid userId, CancellationToken ct);
    
    /// <summary>
    /// Обновить YandexLogin пользователя.
    /// </summary>
    Task<YandexLoginDto> UpdateYandexLoginAsync(Guid userId, string? yandexLogin, CancellationToken ct);
    
    /// <summary>
    /// Получить email пользователя в Яндексе (с доменом).
    /// </summary>
    string? BuildYandexEmail(string? yandexLogin);
    
    /// <summary>
    /// Проверить, требует ли пользователь установки YandexLogin.
    /// </summary>
    Task<bool> RequiresYandexLoginSetupAsync(Guid userId, CancellationToken ct);
}
