using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using CuMusicClub.Application.DTOs.User;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using FluentValidation.Results;
using ValidationException = CuMusicClub.Application.Common.Exceptions.ValidationException;

namespace CuMusicClub.Application.Services.User;

/// <summary>
/// Сервис для управления YandexLogin пользователей.
/// </summary>
public class YandexLoginService : IYandexLoginService
{
    private const string CorporateDomain = "@edu.centraluniversity.ru";
    private const int MaxLength = 100;

    private static readonly Regex LoginRegex = new("^[a-z0-9._-]+$", RegexOptions.Compiled);

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

        var normalized = Normalize(yandexLogin);

        if (normalized != null)
        {
            var owner = await _userRepository.FindByYandexLoginAsync(normalized, ct);
            if (owner != null && owner.Id != userId)
                throw Invalid("Этот логин уже указан у другого участника.");
        }

        user.YandexLogin = normalized;
        await _userRepository.SaveChangesAsync(ct);

        return new YandexLoginDto
        {
            UserId = user.Id,
            YandexLogin = user.YandexLogin
        };
    }

    /// <summary>
    /// «  Ivan.Ivanov@edu.centraluniversity.ru » → «ivan.ivanov»; пустое значение → null (сбросить).
    /// </summary>
    private static string? Normalize(string? yandexLogin)
    {
        if (string.IsNullOrWhiteSpace(yandexLogin))
            return null;

        var login = yandexLogin.Trim().ToLowerInvariant();
        if (login.EndsWith(CorporateDomain, StringComparison.Ordinal))
            login = login[..^CorporateDomain.Length];

        if (login.Length is 0 or > MaxLength)
            throw Invalid($"Логин должен быть от 1 до {MaxLength} символов.");
        if (!LoginRegex.IsMatch(login))
            throw Invalid("Укажите логин корпоративной почты: латинские буквы, цифры и символы . _ -");

        return login;
    }

    private static ValidationException Invalid(string message)
    {
        return new ValidationException([new ValidationFailure("yandexLogin", message)]);
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
