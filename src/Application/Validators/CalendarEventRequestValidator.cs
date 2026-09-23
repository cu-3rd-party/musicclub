using CuMusicClub.Application.DTOs.Calendar;
using FluentValidation;

namespace CuMusicClub.Application.Validators;

/// <summary>
/// Валидатор для запроса создания события календаря.
/// </summary>
public class CalendarEventRequestValidator : AbstractValidator<CreateCalendarEventRequest>
{
    public CalendarEventRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Идентификатор пользователя обязателен");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Заголовок события обязателен")
            .MaximumLength(256)
            .WithMessage("Заголовок не может быть длиннее 256 символов");

        RuleFor(x => x.StartAt)
            .NotEmpty()
            .WithMessage("Дата начала обязательна");

        RuleFor(x => x.EndAt)
            .NotEmpty()
            .WithMessage("Дата окончания обязательна")
            .GreaterThan(x => x.StartAt)
            .WithMessage("Дата окончания должна быть позже даты начала");

        RuleFor(x => x.SourceType)
            .NotEmpty()
            .WithMessage("Тип источника обязателен");

        RuleFor(x => x.EventType)
            .IsInEnum()
            .WithMessage("Недопустимый тип события");
    }
}
