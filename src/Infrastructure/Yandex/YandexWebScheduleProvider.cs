using System.Globalization;
using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Application.Services.Calendar;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Расписание человека через get-events-by-login с opaqueOnly=false
///     (аналог get_detailed_events_for_day из musicscheduler/yandex_api.py).
/// </summary>
public sealed class YandexWebScheduleProvider(IYandexMayaClient maya, IYandexWebSession session)
    : IExternalScheduleProvider
{
    private const string DateFormat = "yyyy-MM-dd";

    public async Task<IReadOnlyList<ExternalScheduleEvent>> GetUserEventsAsync(
        string email,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        // Интеграция не настроена — не дёргаем Яндекс (и Playwright) на каждый запрос страницы
        if (!session.IsConfigured)
            return [];

        var fromMsk = from.UtcDateTime.FromUtcToMsk();
        var toMsk = to.UtcDateTime.FromUtcToMsk();

        var data = await maya.CallAsync("get-events-by-login", new Dictionary<string, object?>
        {
            ["limitAttendees"] = true,
            ["login"] = email,
            ["opaqueOnly"] = false,
            ["email"] = email,
            ["from"] = fromMsk.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            ["to"] = toMsk.Date.AddDays(1).ToString(DateFormat, CultureInfo.InvariantCulture)
        }, ct);

        var result = new List<ExternalScheduleEvent>();
        foreach (var e in YandexWebCalendarOperations.EnumerateEvents(data))
        {
            if (YandexWebCalendarOperations.GetString(e, "decision") == "no"
                || YandexWebCalendarOperations.GetString(e, "availability") == "free")
                continue;

            var start = ToUtc(YandexWebCalendarOperations.ParseYandexTime(
                YandexWebCalendarOperations.GetString(e, "start") ?? YandexWebCalendarOperations.GetString(e, "startTs")));
            var end = ToUtc(YandexWebCalendarOperations.ParseYandexTime(
                YandexWebCalendarOperations.GetString(e, "end") ?? YandexWebCalendarOperations.GetString(e, "endTs")));

            if (start < to && end > from)
                result.Add(new ExternalScheduleEvent(YandexWebCalendarOperations.GetString(e, "name"), start, end));
        }

        return result;
    }

    private static DateTimeOffset ToUtc(DateTime msk)
    {
        return new DateTimeOffset(msk.FromMskToUtc(), TimeSpan.Zero);
    }
}

/// <summary>
///     Заглушка для CalDAV-провайдера: чужие календари по CalDAV не видны.
/// </summary>
public sealed class NullExternalScheduleProvider : IExternalScheduleProvider
{
    public Task<IReadOnlyList<ExternalScheduleEvent>> GetUserEventsAsync(
        string email,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ExternalScheduleEvent>>([]);
    }
}
