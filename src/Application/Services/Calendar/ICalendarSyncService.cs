using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Calendar;

public interface ICalendarSyncService
{
    Task CreateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default);
    Task DeleteBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default);
    Task UpdateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default);
    Task<bool> IsUserBusyAsync(Guid userId, DateTime start, DateTime end, CancellationToken ct = default);
    Task<List<CalDavCalendarInfo>> GetUserCalendarsAsync(string yandexLogin, CancellationToken ct = default);
    /// <summary>
    /// Догоняет календарь: создаёт события для подтверждённых бронирований (в т.ч. прошлых), которых там нет,
    /// и удаляет события отменённых/отклонённых. Ничего не делает, если интеграция выключена.
    /// </summary>
    Task<CalendarBackfillResult> BackfillMissingEventsAsync(CancellationToken ct = default);
    Task UpdateEventParticipantsAsync(
        RehearsalBooking booking,
        IEnumerable<string> newYandexEmails,
        CancellationToken ct = default);
}

public record CalendarBackfillResult(int Created, int Deleted, int Failed);
