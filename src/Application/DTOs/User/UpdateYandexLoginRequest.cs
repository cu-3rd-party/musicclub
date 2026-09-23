using FluentValidation;

namespace CuMusicClub.Application.DTOs.User;

/// <summary>
/// Запрос на обновление YandexLogin пользователя.
/// </summary>
public class UpdateYandexLoginRequest
{
    public string? YandexLogin { get; set; }
}

public class UpdateYandexLoginRequestValidator : AbstractValidator<UpdateYandexLoginRequest>
{
    public UpdateYandexLoginRequestValidator()
    {
        RuleFor(x => x.YandexLogin)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.YandexLogin));
        
        // Проверяем формат: должно быть что-то вроде "ivan.ivanov" или "i.ivanov" (без @edu.centraluniversity.ru)
        RuleFor(x => x.YandexLogin)
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.YandexLogin))
            .WithMessage("YandexLogin должен содержать только латинские буквы, цифры и символы ._-");
    }
}
