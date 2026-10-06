using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace CuMusicClub.Application.Services.Calendar;

public interface ITimetableService
{
    /// <summary>
    /// События для сетки расписания в интервале [from, to).
    /// </summary>
    Task<List<TimetableEventDto>> GetTimetableAsync(
        Guid userId,
        TimetableScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}

/// <summary>
/// Собирает расписание из всех источников: личные события, бронирования репетиций, треклисты и Яндекс.Календарь.
/// </summary>
public class TimetableService(
    ICalendarEventRepository eventRepository,
    IRehearsalBookingRepository bookingRepository,
    ISongRoleAssignmentRepository assignmentRepository,
    ISongRepository songRepository,
    IApplicationUserRepository userRepository,
    IExternalScheduleProvider externalScheduleProvider,
    ILogger<TimetableService> logger) : ITimetableService
{
    /// <summary>
    /// Максимальная длина запрашиваемого интервала: страница показывает день или неделю.
    /// </summary>
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(42);

    public async Task<List<TimetableEventDto>> GetTimetableAsync(
        Guid userId,
        TimetableScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (to <= from || to - from > MaxRange)
            throw new ArgumentException($"Интервал должен быть положительным и не длиннее {MaxRange.TotalDays} дней");

        // Npgsql пишет в timestamptz только UTC-смещение
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();

        var user = await userRepository.FindByIdAsync(userId, ct)
                   ?? throw new NotFoundException(userId.ToString(), nameof(ApplicationUser));

        var result = scope == TimetableScope.Club
            ? await GetClubEventsAsync(user, from, to, ct)
            : await GetMyEventsAsync(user, from, to, ct);

        return result.OrderBy(e => e.StartAt).ThenBy(e => e.EndAt).ToList();
    }

    private async Task<List<TimetableEventDto>> GetMyEventsAsync(
        ApplicationUser user,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var result = new List<TimetableEventDto>();

        // Личные события и события из треклистов, привязанные к пользователю
        var ownEvents = await eventRepository.GetUserActiveEventsAsync(user.Id, from.UtcDateTime, to.UtcDateTime, ct);
        result.AddRange(ownEvents.Select(e => MapCalendarEvent(e, e.EventType == CalendarEventType.Personal)));

        // Репетиции: мои брони, брони моих песен и те, где я роуди
        var mySongIds = (await assignmentRepository.GetSongIdsByUserIdAsync(user.Id, ct)).ToHashSet();
        var bookings = (await bookingRepository.GetActiveInRangeAsync(from, to, ct))
            .Where(b => (user.TgUserId.HasValue && b.RequesterTgUserId == user.TgUserId.Value)
                        || b.RoadieUserId == user.Id
                        || (b.SongId.HasValue && mySongIds.Contains(b.SongId.Value)))
            .ToList();
        result.AddRange(await MapBookingsAsync(bookings, ct));

        // Мой Яндекс.Календарь (пары, встречи) — чтобы видеть, когда я занят
        if (!string.IsNullOrEmpty(user.YandexLogin))
            result.AddRange(await GetExternalEventsAsync(user.YandexLogin, from, to, result, ct));

        return result;
    }

    private async Task<List<TimetableEventDto>> GetClubEventsAsync(
        ApplicationUser user,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var result = new List<TimetableEventDto>();

        var bookings = await bookingRepository.GetActiveInRangeAsync(from, to, ct);
        result.AddRange(await MapBookingsAsync(bookings, ct));

        // События треклистов копируются каждому участнику — схлопываем по источнику
        var clubEvents = await eventRepository.GetActiveClubEventsAsync(from.UtcDateTime, to.UtcDateTime, ct);
        result.AddRange(clubEvents
            .GroupBy(e => e.SourceId.HasValue
                ? $"{e.SourceType}:{e.SourceId}"
                : $"{e.Title}:{e.StartAt:O}")
            .Select(g => MapCalendarEvent(g.First(), false)));

        return result;
    }

    private async Task<List<TimetableEventDto>> MapBookingsAsync(
        IReadOnlyCollection<RehearsalBooking> bookings,
        CancellationToken ct)
    {
        if (bookings.Count == 0)
            return [];

        var songIds = bookings.Where(b => b.SongId.HasValue).Select(b => b.SongId!.Value).Distinct().ToList();
        var titles = songIds.Count > 0
            ? await songRepository.GetTitlesByIdsAsync(songIds, ct)
            : new Dictionary<Guid, string>();

        return bookings.Select(b => new TimetableEventDto
        {
            Id = $"booking:{b.Id}",
            Title = b.SongId.HasValue && titles.TryGetValue(b.SongId.Value, out var title)
                ? title
                : "Репетиция",
            StartAt = b.ScheduledAt,
            EndAt = b.ScheduledAt.AddMinutes(b.DurationMinutes),
            Kind = TimetableEventKind.Rehearsal,
            Location = "Кинотеатр",
            SongId = b.SongId,
            Status = b.Status.ToString(),
            SyncedToCalendar = !string.IsNullOrEmpty(b.CalDavEventUrl)
        }).ToList();
    }

    private async Task<List<TimetableEventDto>> GetExternalEventsAsync(
        string yandexLogin,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyCollection<TimetableEventDto> known,
        CancellationToken ct)
    {
        IReadOnlyList<ExternalScheduleEvent> events;
        try
        {
            events = await externalScheduleProvider.GetUserEventsAsync(
                $"{yandexLogin}@edu.centraluniversity.ru", from, to, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Яндекс недоступен / cookies протухли — показываем хотя бы события клуба
            logger.LogWarning(ex, "Не удалось получить Яндекс.Календарь пользователя {YandexLogin}", yandexLogin);
            return [];
        }

        return events
            // Репетиции бота уже есть в календаре участника как приглашения — не дублируем
            .Where(e => !known.Any(k => k.StartAt == e.Start && k.EndAt == e.End))
            .Select(e => new TimetableEventDto
            {
                Id = $"external:{e.Start.ToUnixTimeSeconds()}:{e.End.ToUnixTimeSeconds()}:{e.Title}",
                Title = string.IsNullOrWhiteSpace(e.Title) ? "Занят" : e.Title.Trim(),
                StartAt = e.Start,
                EndAt = e.End,
                Kind = TimetableEventKind.External
            })
            .ToList();
    }

    private static TimetableEventDto MapCalendarEvent(CalendarEvent e, bool canDelete)
    {
        return new TimetableEventDto
        {
            Id = canDelete ? e.Id.ToString() : $"event:{e.Id}",
            Title = e.Title,
            StartAt = DateTime.SpecifyKind(e.StartAt, DateTimeKind.Utc),
            EndAt = DateTime.SpecifyKind(e.EndAt, DateTimeKind.Utc),
            Kind = e.EventType == CalendarEventType.Personal
                ? TimetableEventKind.Personal
                : TimetableEventKind.Performance,
            Location = e.Location,
            CanDelete = canDelete
        };
    }
}
