using System.Runtime.InteropServices;

namespace CuMusicClub.Application.Common.Extensions;

public static class DateExtensions
{
    public static DateTime ToDateTime(this DateOnly dateOnly) => dateOnly.ToDateTime(TimeOnly.MinValue);

    public static DateTime FromMskToUtc(this DateTime moscowTime)
    {
        string timeZoneId;

        if (moscowTime.Kind == DateTimeKind.Utc)
        {
            return moscowTime;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            timeZoneId = "Russian Standard Time";
        }
        else
        {
            timeZoneId = "Europe/Moscow";
        }

        if (moscowTime.Kind != DateTimeKind.Unspecified)
        {
            moscowTime = DateTime.SpecifyKind(moscowTime, DateTimeKind.Unspecified);
        }

        var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTimeToUtc(moscowTime, moscowTimeZone);
    }

    public static DateTime FromUtcToMsk(this DateTime utcTime)
    {
        string timeZoneId;

        if (utcTime.Kind != DateTimeKind.Utc)
        {
            return utcTime;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            timeZoneId = "Russian Standard Time";
        }
        else
        {
            timeZoneId = "Europe/Moscow";
        }

        var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, moscowTimeZone);
    }
}
