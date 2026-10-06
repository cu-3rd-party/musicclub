namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Личное расписание человека во внешнем календаре (Яндекс), видимое по рабочему email.
/// </summary>
public interface IExternalScheduleProvider
{
    /// <summary>
    /// События пользователя в интервале. Пустой список, если провайдер не умеет смотреть чужие календари.
    /// </summary>
    Task<IReadOnlyList<ExternalScheduleEvent>> GetUserEventsAsync(
        string email,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

/// <summary>
/// Событие из внешнего календаря. Title может быть пустым, если событие скрыто настройками приватности.
/// </summary>
public record ExternalScheduleEvent(string? Title, DateTimeOffset Start, DateTimeOffset End);
