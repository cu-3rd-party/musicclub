using System.Text.Json.Serialization;

namespace CuMusicClub.Application.DTOs.Calendar;

/// <summary>
/// Источник события в расписании.
/// </summary>
public enum TimetableEventKind
{
    /// <summary>Бронирование репетиции через бота.</summary>
    Rehearsal,

    /// <summary>Выступление/репетиция из треклиста.</summary>
    Performance,

    /// <summary>Личное событие, созданное в приложении.</summary>
    Personal,

    /// <summary>Событие из Яндекс.Календаря пользователя.</summary>
    External
}

/// <summary>
/// Событие для отображения в сетке расписания на /app/calendar.
/// </summary>
public class TimetableEventDto
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<TimetableEventKind>))]
    public TimetableEventKind Kind { get; set; }

    public string? Location { get; set; }

    public Guid? SongId { get; set; }

    /// <summary>
    /// Статус бронирования (Pending/Approved/Confirmed) — только для репетиций.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Можно ли удалить событие (только свои личные события).
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Для репетиций: есть ли событие в Яндекс.Календаре (false — пока только в боте).
    /// </summary>
    public bool? SyncedToCalendar { get; set; }
}

public enum TimetableScope
{
    /// <summary>Мои события: личные, репетиции моих песен, мой Яндекс.Календарь.</summary>
    Mine,

    /// <summary>Все события музклуба: репетиции и выступления.</summary>
    Club
}
