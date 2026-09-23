using System.Globalization;
using System.Text;
using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Генератор ICS-фидов в формате RFC 5545.
/// </summary>
public class IcsFeedGenerator : IIcsFeedGenerator
{
    public string GenerateIcs(List<CalendarEventDto> events, string productName, string calendarName)
    {
        var builder = new StringBuilder();
        
        builder.AppendLine("BEGIN:VCALENDAR");
        builder.AppendLine("VERSION:2.0");
        builder.AppendLine($"PRODID:{EscapeText(productName)}");
        builder.AppendLine($"X-WR-CALNAME:{EscapeText(calendarName)}");
        builder.AppendLine("CALSCALE:GREGORIAN");
        builder.AppendLine("METHOD:PUBLISH");

        foreach (var evt in events)
        {
            builder.AppendLine("BEGIN:VEVENT");
            builder.AppendLine($"UID:{evt.Id}@musicclub");
            builder.AppendLine($"DTSTAMP:{FormatDateTime(DateTimeOffset.UtcNow)}");
            builder.AppendLine($"DTSTART:{FormatDateTime(evt.StartAt)}");
            builder.AppendLine($"DTEND:{FormatDateTime(evt.EndAt)}");
            builder.AppendLine($"SUMMARY:{EscapeText(evt.Title)}");
            
            if (!string.IsNullOrEmpty(evt.Description))
            {
                builder.AppendLine($"DESCRIPTION:{EscapeText(evt.Description)}");
            }
            
            if (!string.IsNullOrEmpty(evt.Location))
            {
                builder.AppendLine($"LOCATION:{EscapeText(evt.Location)}");
            }

            builder.AppendLine($"CATEGORIES:{FormatEventType(evt.EventType)}");
            builder.AppendLine("END:VEVENT");
        }

        builder.AppendLine("END:VCALENDAR");

        return builder.ToString();
    }

    /// <summary>
    /// Экранирование специальных символов согласно RFC 5545.
    /// </summary>
    private static string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    /// <summary>
    /// Форматирование даты в формате RFC 5545 (YYYYMMDDTHHMMSSZ).
    /// </summary>
    private static string FormatDateTime(DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyyMMdd\\THHmmss\\Z", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Форматирование типа события в строку.
    /// </summary>
    private static string FormatEventType(CalendarEventType eventType)
    {
        return eventType switch
        {
            CalendarEventType.Rehearsal => "REHEARSAL",
            CalendarEventType.Performance => "PERFORMANCE",
            CalendarEventType.Personal => "PERSONAL",
            _ => eventType.ToString().ToUpperInvariant()
        };
    }
}
