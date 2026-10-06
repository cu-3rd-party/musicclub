using System.Globalization;
using System.Text.Json;
using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Application.Services.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Реализация <see cref="ICalDavOperations" /> через веб-API Яндекс.Календаря (модели Maya) от имени пользователя.
///     Используются только модели, подсмотренные в musicscheduler/yandex_api.py:
///     <c>get-events</c>, <c>get-events-by-login</c>, <c>create-event</c>, <c>delete-event</c>.
///     <para>
///         «URL» здесь — синтетические ссылки, чтобы не менять доменную модель (CalDavEventUrl/CalendarUrl):
///         календарь — <c>yandex-web://layer/{layerId}</c>,
///         событие — <c>yandex-web://layer/{layerId}/event/{eventId}/{yyyy-MM-dd}</c> (дата нужна, чтобы найти событие через get-events).
///     </para>
///     <para>
///         Время: на вход Unspecified = МСК, Utc = UTC (как в <see cref="DateExtensions" />); на выход — МСК (Unspecified).
///     </para>
/// </summary>
public sealed class YandexWebCalendarOperations(
    IYandexMayaClient maya,
    IOptions<YandexWebCalendarOptions> options,
    ILogger<YandexWebCalendarOperations> logger) : ICalDavOperations
{
    private const string Scheme = "yandex-web://";
    private const string LocalFormat = "yyyy-MM-dd'T'HH:mm:ss";
    private const string DateFormat = "yyyy-MM-dd";

    private readonly YandexWebCalendarOptions _options = options.Value;

    public Task<List<CalDavCalendarInfo>> GetCalendarsAsync(CancellationToken ct = default)
    {
        // Список слоёв через веб-API не реверсили — работаем с одним заранее созданным календарём из конфига
        var calendars = _options.LayerId is { } layerId
            ? [new CalDavCalendarInfo(CalendarUrl(layerId), _options.LayerName, null, null)]
            : new List<CalDavCalendarInfo>();

        return Task.FromResult(calendars);
    }

    public Task<CalDavCalendarInfo> CreateCalendarAsync(string displayName, string? color = null,
        CancellationToken ct = default)
    {
        throw new NotSupportedException(
            $"Создание календаря через веб-API Яндекса не поддерживается. Создайте календарь «{displayName}» " +
            "в calendar.yandex.ru вручную и укажите его ID в YandexCalendar__LayerId.");
    }

    public async Task<CalDavEventInfo> CreateEventAsync(string calendarUrl, CalDavEventInfo eventInfo,
        CancellationToken ct = default)
    {
        var layerId = ParseCalendarUrl(calendarUrl);
        var start = ToMsk(eventInfo.Start);
        var end = ToMsk(eventInfo.End);

        var attendees = eventInfo.Participants
            .Where(p => p.Role != CalDavParticipantRole.Optional && IsEmail(p.Email))
            .Select(p => p.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var optionalAttendees = eventInfo.Participants
            .Where(p => p.Role == CalDavParticipantRole.Optional && IsEmail(p.Email))
            .Select(p => p.Email)
            .Except(attendees, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var parameters = new Dictionary<string, object?>
        {
            ["name"] = eventInfo.Title,
            ["eventType"] = "user",
            ["description"] = eventInfo.Description ?? string.Empty,
            ["descriptionHtml"] = eventInfo.Description ?? string.Empty,
            ["location"] = eventInfo.Location ?? string.Empty,
            ["locationHtml"] = eventInfo.Location ?? string.Empty,
            ["attendees"] = attendees,
            ["optionalAttendees"] = optionalAttendees,
            ["attachments"] = Array.Empty<object>(),
            ["availability"] = "busy",
            ["participantsCanInvite"] = true,
            ["isAllDay"] = eventInfo.IsAllDay,
            ["participantsCanEdit"] = false,
            ["visibility"] = "participants",
            ["layerId"] = layerId,
            ["start"] = start.ToString(LocalFormat, CultureInfo.InvariantCulture),
            ["end"] = end.ToString(LocalFormat, CultureInfo.InvariantCulture),
            ["tz"] = _options.TimeZone,
            // externalId каждый раз новый: после delete-event Яндекс может помнить старый UID
            ["externalId"] = $"{Guid.NewGuid():N}yandex.ru",
            ["_meta"] = new Dictionary<string, object>
            {
                ["instanceStartTs"] = ToMsk(DateTime.UtcNow).ToString(LocalFormat, CultureInfo.InvariantCulture),
                ["attendeesCount"] = attendees.Count + optionalAttendees.Count,
                ["addedResourceIndexes"] = Array.Empty<object>()
            },
            ["eventData"] = new { },
            ["marks"] = new { },
            ["notifications"] = attendees.Count > 0
                ? new object[] { new { channel = "email", offset = "-60m" } }
                : Array.Empty<object>()
        };

        var data = await maya.CallAsync("create-event", parameters, ct);

        var eventId = GetString(data, "showEventId") ?? GetString(data, "id");
        if (eventId == null)
        {
            // Ответ без id — ищем только что созданное событие по времени и названию
            var created = (await GetLayerEventsAsync(layerId, start.Date, start.Date.AddDays(1), ct))
                .FirstOrDefault(e => e.Title == eventInfo.Title && e.Start == start);
            eventId = created == null ? null : ParseEventUrl(created.Url!).EventId;
        }

        if (eventId == null)
            throw new InvalidOperationException("Яндекс создал событие, но не вернул его id");

        logger.LogInformation("✅ Создано событие {EventId} в календаре {LayerId}", eventId, layerId);

        return eventInfo with
        {
            Start = start,
            End = end,
            Url = EventUrl(layerId, eventId, start),
            ETag = null
        };
    }

    /// <summary>
    ///     Модель update-event не реверсили, поэтому обновление = создать новое + удалить старое.
    ///     URL события меняется — вызывающий код должен сохранить <see cref="CalDavEventInfo.Url" /> из результата.
    /// </summary>
    public async Task<CalDavEventInfo> UpdateEventAsync(CalDavEventInfo eventInfo, CancellationToken ct = default)
    {
        var oldUrl = eventInfo.Url ?? throw new InvalidOperationException("Event URL is required");
        var (layerId, _, _) = ParseEventUrl(oldUrl);

        var created = await CreateEventAsync(CalendarUrl(layerId), eventInfo, ct);

        try
        {
            await DeleteEventAsync(oldUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "⚠️ Новое событие создано, но старое {EventUrl} удалить не удалось", oldUrl);
        }

        return created;
    }

    public async Task DeleteEventAsync(string eventUrl, CancellationToken ct = default)
    {
        var (layerId, eventId, date) = ParseEventUrl(eventUrl);

        var raw = await FindRawEventAsync(layerId, eventId, date, ct);
        if (raw == null)
        {
            logger.LogInformation("Событие {EventUrl} не найдено — считаем удалённым", eventUrl);
            return;
        }

        var instanceStartTs = GetString(raw.Value, "instanceStartTs") ?? GetString(raw.Value, "startTs");
        var sequence = raw.Value.TryGetProperty("sequence", out var seq) && seq.TryGetInt32(out var seqValue)
            ? seqValue
            : 0;

        await maya.CallAsync("delete-event", new Dictionary<string, object?>
        {
            ["id"] = long.TryParse(eventId, out var numericId) ? numericId : eventId,
            ["sequence"] = sequence,
            ["instanceStartTs"] = instanceStartTs,
            ["layerId"] = layerId,
            ["applyToFuture"] = false
        }, ct);

        logger.LogInformation("🗑 Событие {EventId} удалено из календаря {LayerId}", eventId, layerId);
    }

    public async Task<CalDavEventInfo?> GetEventAsync(string eventUrl, CancellationToken ct = default)
    {
        var (layerId, eventId, date) = ParseEventUrl(eventUrl);
        var raw = await FindRawEventAsync(layerId, eventId, date, ct);
        return raw == null ? null : MapEvent(raw.Value, layerId);
    }

    public async Task<List<CalDavEventInfo>> GetEventsAsync(string calendarUrl, DateTime? startDate = null,
        DateTime? endDate = null, CancellationToken ct = default)
    {
        var layerId = ParseCalendarUrl(calendarUrl);
        var from = ToMsk(startDate ?? DateTime.UtcNow).Date;
        var to = endDate.HasValue ? ToMsk(endDate.Value).Date.AddDays(1) : from.AddDays(30);

        return await GetLayerEventsAsync(layerId, from, to, ct);
    }

    /// <summary>
    ///     Занятость пользователя через get-events-by-login (видно чужое расписание по рабочему email).
    ///     Принимает email пользователя; для URL календаря бота проверяет события в слое.
    /// </summary>
    public async Task<bool> IsUserBusyAsync(string calendarUrl, DateTime start, DateTime end,
        CancellationToken ct = default)
    {
        var from = ToMsk(start);
        var to = ToMsk(end);

        if (calendarUrl.StartsWith(Scheme, StringComparison.Ordinal))
        {
            var events = await GetEventsAsync(calendarUrl, from, to, ct);
            return events.Any(e => e.Start < to && e.End > from);
        }

        var data = await maya.CallAsync("get-events-by-login", new Dictionary<string, object?>
        {
            ["limitAttendees"] = true,
            ["login"] = calendarUrl,
            ["opaqueOnly"] = true,
            ["email"] = calendarUrl,
            ["from"] = from.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            ["to"] = to.Date.AddDays(1).ToString(DateFormat, CultureInfo.InvariantCulture)
        }, ct);

        foreach (var e in EnumerateEvents(data))
        {
            if (GetString(e, "decision") == "no" || GetString(e, "availability") == "free")
                continue;

            var busyStart = ParseYandexTime(GetString(e, "start") ?? GetString(e, "startTs"));
            var busyEnd = ParseYandexTime(GetString(e, "end") ?? GetString(e, "endTs"));
            if (busyStart < to && busyEnd > from)
                return true;
        }

        return false;
    }

    private async Task<List<CalDavEventInfo>> GetLayerEventsAsync(long layerId, DateTime from, DateTime to,
        CancellationToken ct)
    {
        var data = await QueryLayerAsync(layerId, from, to, ct);
        return EnumerateEvents(data).Select(e => MapEvent(e, layerId)).ToList();
    }

    private async Task<JsonElement?> FindRawEventAsync(long layerId, string eventId, DateTime date,
        CancellationToken ct)
    {
        var data = await QueryLayerAsync(layerId, date, date.AddDays(1), ct);
        foreach (var e in EnumerateEvents(data))
            if (GetString(e, "id") == eventId)
                return e;

        return null;
    }

    private Task<JsonElement> QueryLayerAsync(long layerId, DateTime from, DateTime to, CancellationToken ct)
    {
        return maya.CallAsync("get-events", new Dictionary<string, object?>
        {
            ["layerId"] = new[] { layerId.ToString(CultureInfo.InvariantCulture) },
            ["limitAttendees"] = true,
            ["from"] = from.ToString(DateFormat, CultureInfo.InvariantCulture),
            ["to"] = to.ToString(DateFormat, CultureInfo.InvariantCulture)
        }, ct);
    }

    private CalDavEventInfo MapEvent(JsonElement e, long layerId)
    {
        var start = ParseYandexTime(GetString(e, "startTs") ?? GetString(e, "start"));
        var end = ParseYandexTime(GetString(e, "endTs") ?? GetString(e, "end"));
        var id = GetString(e, "id") ?? string.Empty;

        return new CalDavEventInfo(
            GetString(e, "externalId") ?? id,
            GetString(e, "name")?.Trim() ?? string.Empty,
            GetString(e, "description"),
            start,
            end,
            e.TryGetProperty("isAllDay", out var allDay) && allDay.ValueKind == JsonValueKind.True,
            GetString(e, "location"),
            null,
            EventUrl(layerId, id, start),
            start,
            MapAttendees(e));
    }

    private static List<CalDavParticipant> MapAttendees(JsonElement e)
    {
        var result = new List<CalDavParticipant>();

        // С limitAttendees=true Яндекс может отдать не всех; формат подсмотрен не до конца — читаем мягко
        foreach (var (property, role) in new[]
                 {
                     ("attendees", CalDavParticipantRole.Required),
                     ("optionalAttendees", CalDavParticipantRole.Optional)
                 })
        {
            if (!e.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var a in list.EnumerateArray())
            {
                var email = a.ValueKind == JsonValueKind.String ? a.GetString() : GetString(a, "email");
                if (string.IsNullOrEmpty(email))
                    continue;

                var status = GetString(a, "decision") switch
                {
                    "yes" => CalDavParticipantStatus.Accepted,
                    "no" => CalDavParticipantStatus.Declined,
                    "maybe" => CalDavParticipantStatus.Tentative,
                    _ => CalDavParticipantStatus.NeedsAction
                };
                result.Add(new CalDavParticipant(email, GetString(a, "name"), role, status));
            }
        }

        return result;
    }

    internal static IEnumerable<JsonElement> EnumerateEvents(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("events", out var events)
            && events.ValueKind == JsonValueKind.Array)
            return events.EnumerateArray();

        return [];
    }

    internal static string? GetString(JsonElement e, string property)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    /// <summary>
    ///     Время от Яндекса: либо с Z/смещением, либо «наивное» локальное (МСК), как разбирал yandex_api.py.
    /// </summary>
    internal static DateTime ParseYandexTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return default;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
            && (value.EndsWith('Z') || value.LastIndexOfAny(['+', '-']) > 10))
            return DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Utc).FromUtcToMsk();

        return DateTime.SpecifyKind(DateTime.Parse(value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
    }

    private static DateTime ToMsk(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(value.FromUtcToMsk(), DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime().FromUtcToMsk(),
                DateTimeKind.Unspecified),
            _ => value
        };
    }

    private static bool IsEmail(string value)
    {
        return value.Contains('@');
    }

    private static string CalendarUrl(long layerId)
    {
        return $"{Scheme}layer/{layerId}";
    }

    private static string EventUrl(long layerId, string eventId, DateTime start)
    {
        return $"{Scheme}layer/{layerId}/event/{eventId}/{start.ToString(DateFormat, CultureInfo.InvariantCulture)}";
    }

    private static long ParseCalendarUrl(string calendarUrl)
    {
        var parts = SplitUrl(calendarUrl);
        if (parts.Length < 2 || parts[0] != "layer" || !long.TryParse(parts[1], out var layerId))
            throw new ArgumentException($"Некорректный URL календаря Яндекса: {calendarUrl}", nameof(calendarUrl));

        return layerId;
    }

    private static (long LayerId, string EventId, DateTime Date) ParseEventUrl(string eventUrl)
    {
        var parts = SplitUrl(eventUrl);
        if (parts.Length != 5 || parts[0] != "layer" || parts[2] != "event"
            || !long.TryParse(parts[1], out var layerId)
            || !DateTime.TryParseExact(parts[4], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var date))
            throw new ArgumentException($"Некорректный URL события Яндекса: {eventUrl}", nameof(eventUrl));

        return (layerId, parts[3], date);
    }

    private static string[] SplitUrl(string url)
    {
        if (!url.StartsWith(Scheme, StringComparison.Ordinal))
            throw new ArgumentException($"Ожидался URL вида {Scheme}...: {url}", nameof(url));

        return url[Scheme.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
    }
}
