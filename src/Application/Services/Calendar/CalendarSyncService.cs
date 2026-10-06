using System.Text;
using CuMusicClub.Application.Services.User;
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
    ICalendarIntegration integration,
    IApplicationUserRepository userRepository,
    IRehearsalBookingRepository bookingRepository,
    ISongRepository songRepository,
    IYandexEmailSearchService emailSearchService,
    ILogger<CalendarSyncService> logger) : ICalendarSyncService
{
    private const string SharedCalendarDisplayName = "MusicClub — Репетиции";
    private const string OrganizerEmail = "musicclub";

    /// <summary>
    /// Пауза между запросами backfill, чтобы не упереться в лимиты Яндекса.
    /// </summary>
    private static readonly TimeSpan BackfillDelay = TimeSpan.FromMilliseconds(500);

    public Task CreateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default)
    {
        return CreateBookingEventAsync(booking, true, ct);
    }

    private async Task CreateBookingEventAsync(RehearsalBooking booking, bool inviteParticipants,
        CancellationToken ct)
    {
        if (!integration.IsSyncEnabled)
        {
            logger.LogDebug("Синхронизация с календарём выключена — бронирование {BookingId} только в боте", booking.Id);
            return;
        }

        // Уже в календаре (например, backfill и подтверждение пересеклись) — второй раз не создаём
        if (!string.IsNullOrEmpty(booking.CalDavEventUrl))
            return;

        var calendarUrl = await EnsureSharedCalendarExistsAsync(ct);

        // Прошлые репетиции кладём без участников, чтобы не рассылать приглашения задним числом
        var participants = inviteParticipants
            ? await BuildParticipantsForBookingAsync(booking, ct)
            : [];

        var icalUid = $"rehearsal-{booking.Id}@musicclub";

        var eventInfo = new CalDavEventInfo(
            icalUid,
            await BuildEventTitleAsync(booking, ct),
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
            logger.LogDebug("Бронирование {BookingId} не было в календаре — удалять нечего", booking.Id);
            return;
        }

        // Ссылку не трогаем: backfill удалит событие, когда интеграцию снова включат
        if (!integration.IsSyncEnabled)
            return;

        try
        {
            await calDavOperations.DeleteEventAsync(booking.CalDavEventUrl, ct);
            logger.LogInformation("Удалено CalDAV-событие для бронирования {BookingId}", booking.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ссылку оставляем — backfill попробует удалить ещё раз
            logger.LogWarning(ex, "Не удалось удалить CalDAV-событие для бронирования {BookingId}", booking.Id);
            return;
        }

        booking.CalDavEventUrl = null;
        booking.CalDavEventETag = null;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
    }

    public async Task UpdateBookingEventAsync(RehearsalBooking booking, CancellationToken ct = default)
    {
        if (!integration.IsSyncEnabled)
            return;

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
            await BuildEventTitleAsync(booking, ct),
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

        if (!integration.IsSyncEnabled)
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

    public async Task<CalendarBackfillResult> BackfillMissingEventsAsync(CancellationToken ct = default)
    {
        if (!integration.IsSyncEnabled)
            return new CalendarBackfillResult(0, 0, 0);

        int created = 0, deleted = 0, failed = 0;
        var now = DateTimeOffset.UtcNow;

        // Подтверждённые бронирования, которых нет в календаре: созданные, пока интеграция была выключена,
        // или те, где Яндекс в момент бронирования не ответил
        var missing = (await bookingRepository.GetAllByStatusAsync(BookingStatus.Confirmed, ct))
            .Where(b => string.IsNullOrEmpty(b.CalDavEventUrl))
            .ToList();

        foreach (var booking in missing)
        {
            try
            {
                var isPast = booking.ScheduledAt.AddMinutes(booking.DurationMinutes) < now;
                await CreateBookingEventAsync(booking, !isPast, ct);
                await bookingRepository.SaveChangesAsync(ct);
                created++;
                logger.LogInformation("Backfill: бронирование {BookingId} ({ScheduledAt:u}) добавлено в календарь",
                    booking.Id, booking.ScheduledAt);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogError(ex, "Backfill: не удалось добавить бронирование {BookingId} в календарь", booking.Id);
            }

            await Task.Delay(BackfillDelay, ct);
        }

        // Отменённые/отклонённые, чьи события остались в календаре (отменили, пока интеграция была выключена)
        var stale = (await bookingRepository.GetAllByStatusAsync(BookingStatus.Cancelled, ct))
            .Concat(await bookingRepository.GetAllByStatusAsync(BookingStatus.Rejected, ct))
            .Where(b => !string.IsNullOrEmpty(b.CalDavEventUrl))
            .ToList();

        foreach (var booking in stale)
        {
            await DeleteBookingEventAsync(booking, ct);
            await bookingRepository.SaveChangesAsync(ct);

            if (string.IsNullOrEmpty(booking.CalDavEventUrl))
                deleted++;
            else
                failed++;

            await Task.Delay(BackfillDelay, ct);
        }

        return new CalendarBackfillResult(created, deleted, failed);
    }

    public async Task UpdateEventParticipantsAsync(
        RehearsalBooking booking,
        IEnumerable<string> newYandexEmails,
        CancellationToken ct = default)
    {
        if (!integration.IsSyncEnabled || string.IsNullOrEmpty(booking.CalDavEventUrl))
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
        if (requester != null)
        {
            var email = await GetUserEmailAsync(requester, ct);
            if (!string.IsNullOrEmpty(email))
            {
                participants.Add(new CalDavParticipant(
                    email,
                    requester.DisplayName,
                    CalDavParticipantRole.Required,
                    CalDavParticipantStatus.NeedsAction));
            }
        }

        // Роуди
        if (booking.RoadieUserId.HasValue)
        {
            var roadie = await userRepository.FindByIdAsync(booking.RoadieUserId.Value, ct);
            if (roadie != null)
            {
                var email = await GetUserEmailAsync(roadie, ct);
                if (!string.IsNullOrEmpty(email))
                {
                    participants.Add(new CalDavParticipant(
                        email,
                        roadie.DisplayName,
                        CalDavParticipantRole.Required,
                        CalDavParticipantStatus.NeedsAction));
                }
            }
        }

        return participants;
    }

    private async Task<string?> GetUserEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(user.YandexLogin))
            return BuildYandexEmail(user.YandexLogin);

        // Try to search for email by display name
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            try
            {
                var email = await emailSearchService.SearchEmailByNameAsync(user.DisplayName, ct);
                if (!string.IsNullOrEmpty(email))
                    return email;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to search email for user {UserId}", user.Id);
            }
        }

        return null;
    }

    private async Task<string> BuildEventTitleAsync(RehearsalBooking booking, CancellationToken ct)
    {
        var song = booking.SongId is { } songId ? await songRepository.FindByIdAsync(songId, ct) : null;
        return song == null
            ? "🎸 Репетиция"
            : $"🎸 Репетиция: {song.Title} — {song.Artist}";
    }

    private static string BuildEventDescription(RehearsalBooking booking)
    {
        var sb = new StringBuilder();
        var status = booking.Status switch
        {
            BookingStatus.Pending => "ждёт подтверждения",
            BookingStatus.Rejected => "отклонено",
            BookingStatus.Cancelled => "отменено",
            _ => "подтверждено"
        };

        sb.AppendLine("Забронировано через бота Music Club.");
        sb.AppendLine($"Статус: {status}");
        sb.AppendLine($"Длительность: {booking.DurationMinutes} мин");
        sb.AppendLine($"Бронь: {booking.Id:N}");

        return sb.ToString().Trim();
    }

    private static string BuildYandexEmail(string yandexLogin)
        => $"{yandexLogin}@edu.centraluniversity.ru";
}
