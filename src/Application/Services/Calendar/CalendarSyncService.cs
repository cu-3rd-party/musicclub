using System.Text;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сервис синхронизации бронирований с Yandex CalDAV-календарём.
/// Отвечает за создание, обновление и удаление событий, а также backfill при появлении новых Yandex login.
/// </summary>
public class CalendarSyncService(
    ICalDavOperations calDavOperations,
    IApplicationUserRepository userRepository,
    IRehearsalBookingRepository bookingRepository,
    ILogger<CalendarSyncService> logger) : ICalendarSyncService
{
    private const string SharedCalendarDisplayName = "MusicClub — Репетиции";
    private const string OrganizerEmail = "musicclub";

    public async Task CreateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default)
    {
        var calendarUrl = await EnsureSharedCalendarExistsAsync(ct);

        var participants = await BuildParticipantsForBookingAsync(booking, ct);

        var icalUid = $"rehearsal-{booking.Id}@musicclub";

        var eventInfo = new CalDavEventInfo(
            icalUid,
            BuildEventTitle(booking),
            BuildEventDescription(booking),
            booking.ScheduledAt.UtcDateTime,
            booking.ScheduledAt.AddMinutes(booking.DurationMinutes).UtcDateTime,
            false,
            "Кинотеатр",
            null,
            null,
            DateTime.UtcNow,
            participants);

        var created = await calDavOperations.CreateEventAsync(calendarUrl, eventInfo, ct);

        booking.CalDavEventUrl = created.Url;
        booking.CalDavEventETag = created.ETag;
        booking.CalendarUrl = calendarUrl;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        bookingRepository.Update(booking);
    }

    public async Task DeleteBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(booking.CalDavEventUrl))
        {
            logger.LogWarning("Нет CalDAV URL для удаления бронирования {BookingId}", booking.Id);
            return;
        }

        try
        {
            await calDavOperations.DeleteEventAsync(booking.CalDavEventUrl, ct);
            logger.LogInformation("Удалено CalDAV-событие для бронирования {BookingId}", booking.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить CalDAV-событие для бронирования {BookingId}", booking.Id);
        }
        finally
        {
            booking.CalDavEventUrl = null;
            booking.CalDavEventETag = null;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            bookingRepository.Update(booking);
        }
    }

    public async Task UpdateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(booking.CalDavEventUrl))
        {
            await CreateBookingEventAsync(booking, ct);
            return;
        }

        var participants = await BuildParticipantsForBookingAsync(booking, ct);

        var existing = await calDavOperations.GetEventAsync(booking.CalDavEventUrl, ct);
        if (existing == null)
        {
            logger.LogWarning("CalDAV-событие не найдено, создаём новое для бронирования {BookingId}", booking.Id);
            await CreateBookingEventAsync(booking, ct);
            return;
        }

        var updatedInfo = new CalDavEventInfo(
            existing.Uid,
            BuildEventTitle(booking),
            BuildEventDescription(booking),
            booking.ScheduledAt.UtcDateTime,
            booking.ScheduledAt.AddMinutes(booking.DurationMinutes).UtcDateTime,
            false,
            "Кинотеатр",
            existing.ETag,
            existing.Url,
            existing.Created,
            participants);

        var updated = await calDavOperations.UpdateEventAsync(updatedInfo, ct);

        // Реализация может пересоздать событие (веб-API Яндекса) — URL меняется
        booking.CalDavEventUrl = updated.Url ?? booking.CalDavEventUrl;
        booking.CalDavEventETag = updated.ETag;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
    }

    public async Task<bool> IsUserBusyAsync(Guid userId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(userId, ct);
        if (user == null || string.IsNullOrEmpty(user.YandexLogin))
            return false;

        // Веб-API Яндекса видит занятость коллег по рабочему email; CalDAV-реализация вернёт false
        try
        {
            return await calDavOperations.IsUserBusyAsync(BuildYandexEmail(user.YandexLogin), start, end, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось проверить занятость пользователя {UserId}", userId);
            return false;
        }
    }

    public async Task<List<CalDavCalendarInfo>> GetUserCalendarsAsync(string yandexLogin, CancellationToken ct = default)
    {
        return await calDavOperations.GetCalendarsAsync(ct);
    }

    public async Task<int> BackfillMissingEventsAsync(CancellationToken ct = default)
    {
        var allBookings = await bookingRepository.GetAllByStatusAsync(BookingStatus.Confirmed, ct);

        var missingBookings = allBookings
            .Where(b => string.IsNullOrEmpty(b.CalDavEventUrl))
            .ToList();

        var createdCount = 0;

        foreach (var booking in missingBookings)
        {
            try
            {
                await CreateBookingEventAsync(booking, ct);
                createdCount++;
                logger.LogInformation("Backfilled CalDAV event for booking {BookingId}", booking.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to backfill CalDAV event for booking {BookingId}", booking.Id);
            }
        }

        return createdCount;
    }

    public async Task UpdateEventParticipantsAsync(
        RehearsalBooking booking,
        IEnumerable<string> newYandexEmails,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(booking.CalDavEventUrl))
            return;

        var existing = await calDavOperations.GetEventAsync(booking.CalDavEventUrl, ct);
        if (existing == null)
            return;

        var participants = existing.Participants.ToList();
        foreach (var email in newYandexEmails)
        {
            if (!participants.Any(p => p.Email == email))
            {
                participants.Add(new CalDavParticipant(email, null, CalDavParticipantRole.Required, CalDavParticipantStatus.NeedsAction));
            }
        }

        var updatedInfo = new CalDavEventInfo(
            existing.Uid,
            existing.Title,
            existing.Description,
            existing.Start,
            existing.End,
            existing.IsAllDay,
            existing.Location,
            existing.ETag,
            existing.Url,
            existing.Created,
            participants);

        var updated = await calDavOperations.UpdateEventAsync(updatedInfo, ct);

        booking.CalDavEventUrl = updated.Url ?? booking.CalDavEventUrl;
        booking.CalDavEventETag = updated.ETag;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
    }

    private async Task<string> EnsureSharedCalendarExistsAsync(CancellationToken ct)
    {
        var calendars = await calDavOperations.GetCalendarsAsync(ct);

        var sharedCalendar = calendars.FirstOrDefault(c =>
            c.DisplayName?.Contains("MusicClub", StringComparison.OrdinalIgnoreCase) == true
            || c.DisplayName?.Contains("Репетиции", StringComparison.OrdinalIgnoreCase) == true);

        if (sharedCalendar != null)
            return sharedCalendar.Url;

        var created = await calDavOperations.CreateCalendarAsync(SharedCalendarDisplayName, ct: ct);
        logger.LogInformation("Создан общий календарь {CalendarUrl}", created.Url);
        return created.Url;
    }

    private async Task<List<CalDavParticipant>> BuildParticipantsForBookingAsync(
        RehearsalBooking booking,
        CancellationToken ct)
    {
        var participants = new List<CalDavParticipant>();

        // Организатор
        participants.Add(new CalDavParticipant(OrganizerEmail, "MusicClub Bot", CalDavParticipantRole.Required, CalDavParticipantStatus.NeedsAction));

        // Инициатор
        var requester = await userRepository.FindByTgUserIdAsync(booking.RequesterTgUserId, ct);
        if (requester != null && !string.IsNullOrEmpty(requester.YandexLogin))
        {
            participants.Add(new CalDavParticipant(
                BuildYandexEmail(requester.YandexLogin),
                requester.DisplayName,
                CalDavParticipantRole.Required,
                CalDavParticipantStatus.NeedsAction));
        }

        // Илья (тренер)
        if (booking.CoachUserId.HasValue)
        {
            var coach = await userRepository.FindByIdAsync(booking.CoachUserId.Value, ct);
            if (coach != null && !string.IsNullOrEmpty(coach.YandexLogin))
            {
                participants.Add(new CalDavParticipant(
                    BuildYandexEmail(coach.YandexLogin),
                    coach.DisplayName,
                    CalDavParticipantRole.Required,
                    CalDavParticipantStatus.NeedsAction));
            }
        }

        return participants;
    }

    private static string BuildEventTitle(RehearsalBooking booking)
    {
        if (booking.SongId.HasValue)
            return $"🎸 Репетиция (ID: {booking.SongId.Value:N})";
        return "🎸 Репетиция";
    }

    private static string BuildEventDescription(RehearsalBooking booking)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Бронирование: {booking.Id:N}");
        sb.AppendLine($"Статус: {booking.Status}");
        sb.AppendLine($"Длительность: {booking.DurationMinutes} мин");
        sb.AppendLine($"Создано: {booking.CreatedAt:yyyy-MM-dd HH:mm}");

        return sb.ToString().Trim();
    }

    private static string BuildYandexEmail(string yandexLogin)
        => $"{yandexLogin}@edu.centraluniversity.ru";
}
