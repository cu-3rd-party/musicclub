using CuMusicClub.Infrastructure.YandexCalDav.Models;

namespace CuMusicClub.Infrastructure.YandexCalDav;

public interface IYandexCalDavClient
{
    string Organizer { get; }
    Task<List<CalendarInfo>> GetCalendarsAsync();
    Task<CalendarInfo> GetCalendarAsync(string calendarUrl);

    Task<CalendarInfo> CreateCalendarAsync(string displayName, string? color = null,
        CancellationToken cancellationToken = default);

    Task<List<CalendarEventInfo>> GetEventsAsync(string calendarUrl, DateTime? startDate = null,
        DateTime? endDate = null);

    Task<CalendarEventInfo?> GetEventAsync(string eventUrl);
    Task<CalendarEventInfo> CreateEventAsync(string calendarUrl, CalendarEventInfo eventInfo);
    Task<CalendarEventInfo> UpdateEventAsync(CalendarEventInfo eventInfo);
    Task DeleteEventAsync(string eventUrl);
}
