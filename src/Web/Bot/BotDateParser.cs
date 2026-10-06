using System.Globalization;
using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Extensions;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Разбор дат и времени из аргументов команд бота. Все значения — московское время (Kind=Unspecified).
/// </summary>
public static class BotDateParser
{
    public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public const string DateHint = "ДД.ММ, день недели (пн…вс), «сегодня» или «завтра»";

    private static readonly Regex DateRegex = new(@"^(\d{1,2})[./-](\d{1,2})(?:[./-](\d{2}|\d{4}))?$",
        RegexOptions.Compiled);

    private static readonly Regex TimeRegex = new(@"^(\d{1,2})(?:[:.]?(\d{2}))?$", RegexOptions.Compiled);

    private static readonly Dictionary<string, DayOfWeek> DayAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["пн"] = DayOfWeek.Monday, ["пон"] = DayOfWeek.Monday, ["понедельник"] = DayOfWeek.Monday,
        ["mon"] = DayOfWeek.Monday,
        ["вт"] = DayOfWeek.Tuesday, ["вто"] = DayOfWeek.Tuesday, ["вторник"] = DayOfWeek.Tuesday,
        ["tue"] = DayOfWeek.Tuesday,
        ["ср"] = DayOfWeek.Wednesday, ["сре"] = DayOfWeek.Wednesday, ["среда"] = DayOfWeek.Wednesday,
        ["среду"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
        ["чт"] = DayOfWeek.Thursday, ["чет"] = DayOfWeek.Thursday, ["четверг"] = DayOfWeek.Thursday,
        ["thu"] = DayOfWeek.Thursday,
        ["пт"] = DayOfWeek.Friday, ["пят"] = DayOfWeek.Friday, ["пятница"] = DayOfWeek.Friday,
        ["пятницу"] = DayOfWeek.Friday, ["fri"] = DayOfWeek.Friday,
        ["сб"] = DayOfWeek.Saturday, ["суб"] = DayOfWeek.Saturday, ["суббота"] = DayOfWeek.Saturday,
        ["субботу"] = DayOfWeek.Saturday, ["sat"] = DayOfWeek.Saturday,
        ["вс"] = DayOfWeek.Sunday, ["вос"] = DayOfWeek.Sunday, ["воскресенье"] = DayOfWeek.Sunday,
        ["sun"] = DayOfWeek.Sunday,
    };

    /// <summary>
    /// Текущее московское время.
    /// </summary>
    public static DateTime NowMsk()
    {
        return DateTime.UtcNow.FromUtcToMsk();
    }

    /// <summary>
    /// Дата: ДД.ММ[.ГГГГ], день недели, «сегодня»/«завтра»/«послезавтра».
    /// Дата без года, которая уже прошла, считается датой следующего года; день недели — ближайший, включая сегодня.
    /// </summary>
    public static bool TryParseDate(string? token, DateTime today, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(token)) return false;

        token = token.Trim().TrimEnd(',');
        today = today.Date;

        switch (token.ToLowerInvariant())
        {
            case "сегодня":
            case "today":
                date = today;
                return true;
            case "завтра":
            case "tomorrow":
                date = today.AddDays(1);
                return true;
            case "послезавтра":
                date = today.AddDays(2);
                return true;
        }

        if (DayAliases.TryGetValue(token, out var dayOfWeek))
        {
            var diff = ((int) dayOfWeek - (int) today.DayOfWeek + 7) % 7;
            date = today.AddDays(diff);
            return true;
        }

        var match = DateRegex.Match(token);
        if (!match.Success) return false;

        var day = int.Parse(match.Groups[1].Value);
        var month = int.Parse(match.Groups[2].Value);
        var hasYear = match.Groups[3].Success;
        var year = hasYear ? int.Parse(match.Groups[3].Value) : today.Year;
        if (year < 100) year += 2000;

        if (!IsValidDate(year, month, day)) return false;

        date = new DateTime(year, month, day);

        // «05.01» в декабре — это январь следующего года
        if (!hasYear && date < today.AddDays(-7) && IsValidDate(year + 1, month, day))
            date = date.AddYears(1);

        return true;
    }

    /// <summary>
    /// Время: ЧЧ, ЧЧ:ММ, ЧЧ.ММ, ЧЧММ. Для диапазона «17-19» или «17:00–19:00» берётся начало.
    /// </summary>
    public static bool TryParseTime(string? token, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var start = token.Trim().Split(['-', '–', '—'], 2)[0];
        var match = TimeRegex.Match(start);
        if (!match.Success) return false;

        var hour = int.Parse(match.Groups[1].Value);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        if (hour is < 0 or > 23 || minute is < 0 or > 59) return false;

        time = new TimeOnly(hour, minute);
        return true;
    }

    /// <summary>
    /// «ДАТА ВРЕМЯ» из аргументов команды. Возвращает false, если чего-то не хватает или формат неверный.
    /// </summary>
    public static bool TryParseDateTime(string? args, DateTime today, out DateTime dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(args)) return false;

        var parts = args.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        if (!TryParseDate(parts[0], today, out var date) || !TryParseTime(parts[1], out var time))
            return false;

        dateTime = date.Add(time.ToTimeSpan());
        return true;
    }

    /// <summary>
    /// «ДД.ММ, пн» — для заголовков.
    /// </summary>
    public static string FormatDate(DateTime date)
    {
        return date.ToString("dd.MM, ddd", Ru);
    }

    /// <summary>
    /// «Пн, 12.10» — для группировки по дням.
    /// </summary>
    public static string FormatDayHeader(DateTime date)
    {
        var dow = date.ToString("ddd", Ru);
        return $"{char.ToUpper(dow[0], Ru)}{dow[1..]}, {date:dd.MM}";
    }

    private static bool IsValidDate(int year, int month, int day)
    {
        return year is >= 2000 and <= 2100
               && month is >= 1 and <= 12
               && day >= 1
               && day <= DateTime.DaysInMonth(year, month);
    }
}
