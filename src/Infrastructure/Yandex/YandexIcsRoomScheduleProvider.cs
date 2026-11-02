using CuMusicClub.Application.Services.Calendar;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Расписание зала из приватного ICS-экспорта Яндекс.Календаря
///     (аналог get_events_via_ics(REP_BASE_TOKEN) из musicscheduler/yandex_api.py).
///     Повторяющиеся события разворачиваются через Ical.Net.
/// </summary>
public sealed class YandexIcsRoomScheduleProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<YandexWebCalendarOptions> options) : IRoomScheduleProvider
{
    public const string HttpClientName = "yandex-room-ics";

    private readonly YandexWebCalendarOptions _options = options.Value;

    public bool IsConfigured
    {
        get
        {
            return !string.IsNullOrWhiteSpace(_options.RoomIcsToken);
        }
    }

    public async Task<IReadOnlyList<ExternalScheduleEvent>> GetRoomEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return [];

        var url = "https://calendar.yandex.ru/export/ics.xml?private_token="
                  + Uri.EscapeDataString(_options.RoomIcsToken!.Trim());

        var client = httpClientFactory.CreateClient(HttpClientName);
        var ics = await client.GetStringAsync(url, ct);

        return ParseEvents(ics, from, to);
    }

    public static List<ExternalScheduleEvent> ParseEvents(string ics, DateTimeOffset from, DateTimeOffset to)
    {
        var calendar = Calendar.Load(ics);

        var result = new List<ExternalScheduleEvent>();
        foreach (var occurrence in calendar.GetOccurrences(new CalDateTime(from.UtcDateTime, "UTC"),
                     new CalDateTime(to.UtcDateTime, "UTC")))
        {
            if (occurrence.Source is not CalendarEvent calendarEvent)
                continue;
            if (string.Equals(calendarEvent.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(calendarEvent.Transparency, "TRANSPARENT", StringComparison.OrdinalIgnoreCase))
                continue;

            var start = new DateTimeOffset(occurrence.Period.StartTime.AsUtc, TimeSpan.Zero);
            var end = occurrence.Period.EndTime != null
                ? new DateTimeOffset(occurrence.Period.EndTime.AsUtc, TimeSpan.Zero)
                : start + (occurrence.Period.Duration > TimeSpan.Zero
                    ? occurrence.Period.Duration
                    : calendarEvent.Duration);

            if (start < to && end > from)
                result.Add(new ExternalScheduleEvent(
                    calendarEvent.Summary,
                    start,
                    end,
                    GetLogin(calendarEvent.Organizer?.Value),
                    GetAttendees(calendarEvent)));
        }

        return result.OrderBy(e => e.Start).ToList();
    }

    private static List<ExternalScheduleAttendee> GetAttendees(CalendarEvent calendarEvent)
    {
        return calendarEvent.Attendees
            .Select(a => (Login: GetLogin(a.Value), Status: a.ParticipationStatus))
            .Where(a => a.Login != null)
            .Select(a => new ExternalScheduleAttendee(
                a.Login!,
                string.Equals(a.Status, "DECLINED", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Логин — часть адреса до @ (mailto:i.ivanov@example.com → i.ivanov).</summary>
    private static string? GetLogin(Uri? address)
    {
        if (address == null)
            return null;

        var value = address.ToString();
        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            value = value["mailto:".Length..];

        var at = value.IndexOf('@');
        return at > 0 ? value[..at] : null;
    }
}
