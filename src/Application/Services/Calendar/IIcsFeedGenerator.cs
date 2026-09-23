using CuMusicClub.Application.DTOs.Calendar;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Генератор ICS-фидов в формате RFC 5545.
/// </summary>
public interface IIcsFeedGenerator
{
    /// <summary>
    /// Генерирует ICS-строку из списка событий.
    /// </summary>
    /// <param name="events">Список событий.</param>
    /// <param name="productName">Идентификатор продукта (PRODID).</param>
    /// <param name="calendarName">Название календаря (X-WR-CALNAME).</param>
    string GenerateIcs(List<CalendarEventDto> events, string productName, string calendarName);
}
