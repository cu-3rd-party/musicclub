using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сводка дня для /day: сопоставляет календарь зала, брони бота и календари участников песни
/// (аналог cmd_day + parse_room_events из musicscheduler).
/// </summary>
public interface IDayScheduleService
{
    Task<DaySchedule> GetDayAsync(
        DateOnly date,
        Guid? songId,
        IReadOnlyCollection<string> excludeUsernames,
        CancellationToken ct = default);
}

public enum RoomSlotStatus
{
    Free,
    Coach,
    Busy
}

public record RoomTimelineItem(DateTimeOffset Start, DateTimeOffset End, RoomSlotStatus Status, string Title);

public enum MemberScheduleStatus
{
    Ok,
    NoYandexLogin,
    Error
}

public record MemberSchedule(
    string Name,
    string? UserName,
    MemberScheduleStatus Status,
    IReadOnlyList<ExternalScheduleEvent> Events);

public record FreeWindow(DateTimeOffset Start, DateTimeOffset End, bool WithCoach);

public record DaySchedule(
    DateOnly Date,
    bool RoomKnown,
    IReadOnlyList<RoomTimelineItem> Room,
    IReadOnlyList<RehearsalBooking> Bookings,
    IReadOnlyList<MemberSchedule> Members,
    IReadOnlyList<FreeWindow> FreeWindows);

public class DayScheduleService(
    IRoomScheduleProvider roomScheduleProvider,
    IExternalScheduleProvider externalScheduleProvider,
    IRehearsalBookingRepository bookingRepository,
    ISongRoleAssignmentRepository songRoleAssignmentRepository,
    IApplicationUserRepository userRepository,
    ILogger<DayScheduleService> logger) : IDayScheduleService
{
    public const int WorkStartHour = 10;
    public const int WorkEndHour = 22;
    private static readonly TimeSpan MinFreeWindow = TimeSpan.FromMinutes(60);

    public async Task<DaySchedule> GetDayAsync(
        DateOnly date,
        Guid? songId,
        IReadOnlyCollection<string> excludeUsernames,
        CancellationToken ct = default)
    {
        var dayFrom = MskToUtc(date, 0);
        var dayTo = MskToUtc(date.AddDays(1), 0);
        var workFrom = MskToUtc(date, WorkStartHour);
        var workTo = MskToUtc(date, WorkEndHour);

        var roomKnown = roomScheduleProvider.IsConfigured;
        IReadOnlyList<ExternalScheduleEvent> roomEvents = [];
        if (roomKnown)
        {
            try
            {
                roomEvents = await roomScheduleProvider.GetRoomEventsAsync(dayFrom, dayTo, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось получить расписание зала на {Date}", date);
                roomKnown = false;
            }
        }

        var bookings = await bookingRepository.GetActiveInRangeAsync(dayFrom, dayTo, ct);

        var members = songId is { } id
            ? await GetMembersAsync(id, excludeUsernames, dayFrom, dayTo, ct)
            : [];

        var room = BuildRoomTimeline(roomEvents, bookings, workFrom, workTo);
        var freeWindows = BuildFreeWindows(room, members, workFrom, workTo);

        return new DaySchedule(date, roomKnown, room, bookings, members, freeWindows);
    }

    /// <summary>
    /// Таймлайн зала в рабочих часах: занято (репетиции, BUSY, брони бота) &gt; слот тренера (SLOT) &gt; свободно.
    /// </summary>
    public static List<RoomTimelineItem> BuildRoomTimeline(
        IReadOnlyCollection<ExternalScheduleEvent> roomEvents,
        IReadOnlyCollection<RehearsalBooking> bookings,
        DateTimeOffset workFrom,
        DateTimeOffset workTo)
    {
        var busy = new List<(DateTimeOffset Start, DateTimeOffset End, string Title)>();
        var coach = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        foreach (var e in roomEvents)
        {
            var title = e.Title?.Trim() ?? "";
            if (title.Contains("slot", StringComparison.OrdinalIgnoreCase))
                coach.Add((e.Start, e.End));
            else
                busy.Add((e.Start, e.End, title.Length > 0 ? title : "Занято"));
        }

        foreach (var b in bookings)
            busy.Add((b.ScheduledAt, b.ScheduledAt.AddMinutes(b.DurationMinutes), "Бронь бота"));

        var points = SortedCutPoints(workFrom, workTo,
            busy.Select(b => (b.Start, b.End)).Concat(coach));

        var result = new List<RoomTimelineItem>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var mid = Mid(points[i], points[i + 1]);
            var busyHit = busy.FirstOrDefault(b => b.Start <= mid && mid < b.End);
            var item = busyHit.Title != null
                ? new RoomTimelineItem(points[i], points[i + 1], RoomSlotStatus.Busy, busyHit.Title)
                : coach.Any(c => c.Start <= mid && mid < c.End)
                    ? new RoomTimelineItem(points[i], points[i + 1], RoomSlotStatus.Coach, "Свободно (с роуди)")
                    : new RoomTimelineItem(points[i], points[i + 1], RoomSlotStatus.Free, "Свободно (сам)");

            if (result.Count > 0 && result[^1].Status == item.Status && result[^1].Title == item.Title)
                result[^1] = result[^1] with { End = item.End };
            else
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Окна в рабочих часах, когда зал не занят и у всех участников (с известным календарём) нет событий.
    /// </summary>
    public static List<FreeWindow> BuildFreeWindows(
        IReadOnlyCollection<RoomTimelineItem> room,
        IReadOnlyCollection<MemberSchedule> members,
        DateTimeOffset workFrom,
        DateTimeOffset workTo)
    {
        var memberBusy = members
            .SelectMany(m => m.Events)
            .Select(e => (e.Start, e.End))
            .ToList();

        var points = SortedCutPoints(workFrom, workTo,
            room.Select(r => (r.Start, r.End)).Concat(memberBusy));

        var result = new List<FreeWindow>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var mid = Mid(points[i], points[i + 1]);
            var roomItem = room.FirstOrDefault(r => r.Start <= mid && mid < r.End);
            if (roomItem?.Status == RoomSlotStatus.Busy)
                continue;
            if (memberBusy.Any(b => b.Start <= mid && mid < b.End))
                continue;

            var withCoach = roomItem?.Status == RoomSlotStatus.Coach;
            if (result.Count > 0 && result[^1].End == points[i] && result[^1].WithCoach == withCoach)
                result[^1] = result[^1] with { End = points[i + 1] };
            else
                result.Add(new FreeWindow(points[i], points[i + 1], withCoach));
        }

        return result.Where(w => w.End - w.Start >= MinFreeWindow).ToList();
    }

    private async Task<List<MemberSchedule>> GetMembersAsync(
        Guid songId,
        IReadOnlyCollection<string> excludeUsernames,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var excluded = new HashSet<string>(
            excludeUsernames.Select(u => u.TrimStart('@')),
            StringComparer.OrdinalIgnoreCase);

        var userIds = (await songRoleAssignmentRepository.GetMemberUserIdsBySongIdAsync(songId, ct)).Distinct().ToList();

        var queryable = userRepository.Query()
            .Where(u => userIds.Contains(u.Id));

        var users = await queryable.ToListAsync(ct);

        var result = new List<MemberSchedule>();
        foreach (var user in users)
        {
            if (user.UserName != null && excluded.Contains(user.UserName))
                continue;

            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? "?" : user.DisplayName;

            if (string.IsNullOrWhiteSpace(user.YandexLogin))
            {
                result.Add(new MemberSchedule(name, user.UserName, MemberScheduleStatus.NoYandexLogin, []));
                continue;
            }

            try
            {
                var events = await externalScheduleProvider.GetUserEventsAsync(
                    BuildYandexEmail(user.YandexLogin), from, to, ct);
                result.Add(new MemberSchedule(name, user.UserName, MemberScheduleStatus.Ok,
                    events.OrderBy(e => e.Start).ToList()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось получить расписание {YandexLogin}", user.YandexLogin);
                result.Add(new MemberSchedule(name, user.UserName, MemberScheduleStatus.Error, []));
            }
        }

        return result.OrderBy(m => m.Name).ToList();
    }

    private static List<DateTimeOffset> SortedCutPoints(
        DateTimeOffset from,
        DateTimeOffset to,
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> intervals)
    {
        var points = new HashSet<DateTimeOffset> { from, to };
        foreach (var (start, end) in intervals)
        {
            if (start < to && end > from)
            {
                points.Add(start < from ? from : start);
                points.Add(end > to ? to : end);
            }
        }

        return points.OrderBy(p => p).ToList();
    }

    private static DateTimeOffset Mid(DateTimeOffset a, DateTimeOffset b)
    {
        return a + (b - a) / 2;
    }

    private static DateTimeOffset MskToUtc(DateOnly date, int hour)
    {
        var msk = date.ToDateTime(new TimeOnly(hour, 0));
        return new DateTimeOffset(msk.FromMskToUtc(), TimeSpan.Zero);
    }

    private static string BuildYandexEmail(string yandexLogin)
    {
        var login = yandexLogin.Trim().ToLowerInvariant();
        return login.Contains('@') ? login : $"{login}@edu.centraluniversity.ru";
    }
}
