using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Бронирование репетиции, созданное через Telegram-бота.
/// Отслеживает состояние бронирования и синхронизацию с CalDAV-календарём.
/// </summary>
public class RehearsalBooking
{
    public Guid Id { get; set; }

    /// <summary>
    /// Когда запланирована репетиция.
    /// </summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>
    /// Длительность в минутах (по умолчанию 80).
    /// </summary>
    public int DurationMinutes { get; set; } = 80;

    /// <summary>
    /// Текущий статус бронирования.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Telegram user ID инициатора бронирования.
    /// </summary>
    public long RequesterTgUserId { get; set; }

    /// <summary>
    /// User ID роуди, если требуется его участие.
    /// </summary>
    public Guid? RoadieUserId { get; set; }

    /// <summary>
    /// ID песни, к которой привязана репетиция (опционально).
    /// </summary>
    public Guid? SongId { get; set; }

    /// <summary>
    /// URL созданного события в CalDAV (для синхронизации и удаления).
    /// </summary>
    public string? CalDavEventUrl { get; set; }

    /// <summary>
    /// ETag CalDAV-события для обнаружения конфликтов.
    /// </summary>
    public string? CalDavEventETag { get; set; }

    /// <summary>
    /// URL календаря, в которое создано событие.
    /// </summary>
    public string? CalendarUrl { get; set; }

    /// <summary>
    /// Флаг: событие создано ботом (vs внешнее событие в календаре).
    /// </summary>
    public bool IsBotManaged { get; set; } = true;

    /// <summary>
    /// Когда создано.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Когда последний раз обновлено.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
