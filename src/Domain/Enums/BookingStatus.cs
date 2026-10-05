namespace CuMusicClub.Domain.Enums;

/// <summary>
/// Статус бронирования репетиции.
/// </summary>
public enum BookingStatus
{
    Pending = 1,    // Ожидает подтверждения (Илья)
    Approved = 2,   // Подтверждено Ильёй
    Rejected = 3,   // Отклонено Ильёй
    Cancelled = 4,  // Отменено инициатором
    Confirmed = 5   // Подтверждено и синхронизировано с CalDAV
}
