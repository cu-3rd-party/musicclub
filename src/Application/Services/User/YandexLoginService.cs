using Ardalis.GuardClauses;
using CuMusicClub.Application.DTOs.User;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.User;

/// <summary>
/// Сервис для управления YandexLogin пользователей.
/// </summary>
public class YandexLoginService : IYandexLoginService
{
    private readonly IApplicationUserRepository _userRepository;

    public YandexLoginService(IApplicationUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<YandexLoginDto?> GetYandexLoginAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return null;

        return new YandexLoginDto
        {
            UserId = user.Id,
            YandexLogin = user.YandexLogin
        };
    }

    public async Task<YandexLoginDto> UpdateYandexLoginAsync(Guid userId, string? yandexLogin, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        Guard.Against.NotFound(userId, user, $"Пользователь {userId} не найден");

        user.YandexLogin = yandexLogin;
        await _userRepository.SaveChangesAsync(ct);

        return new YandexLoginDto
        {
            UserId = user.Id,
            YandexLogin = user.YandexLogin
        };
    }

    public string? BuildYandexEmail(string? yandexLogin)
    {
        if (string.IsNullOrWhiteSpace(yandexLogin))
            return null;

        // Проверяем, есть ли уже домен в логине
        if (yandexLogin.Contains('@'))
            return yandexLogin.ToLowerInvariant().Trim();

        // Добавляем стандартный домен
        return $"{yandexLogin.Trim().ToLowerInvariant()}@edu.centraluniversity.ru";
    }

    public async Task<bool> RequiresYandexLoginSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user == null)
            return false;

        // Требуется установка, если YandexLogin не задан
        return string.IsNullOrWhiteSpace(user.YandexLogin);
    }
}
