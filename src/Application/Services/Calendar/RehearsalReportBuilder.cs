using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>Песня глазами отчёта: логины участников и роуди.</summary>
public record RehearsalReportSong(
    string Title,
    string Artist,
    IReadOnlyCollection<string> MemberLogins,
    string? RoadieLogin);

/// <summary>Строка отчёта. Login == null — на репетицию не нашлось ни одного логина.</summary>
public record RehearsalReportRow(string? Login, DateTimeOffset Start);

/// <summary>
/// Собирает строки отчёта о репетициях из календаря зала и составов песен.
/// </summary>
public static partial class RehearsalReportBuilder
{
    private const string Club = "MusicClub";
    private const int MaxSheetNameLength = 31;
    private static readonly TimeSpan Moscow = TimeSpan.FromHours(3);

    /// <summary>
    /// Репетиция — событие зала с заголовком «Репетиция …» (остальное, например BUSY, не считается).
    /// Если в событии есть приглашённые, берём их: это состав на момент репетиции. Отказавшихся
    /// не считаем, как и организатора, инициатора и роуди, если они не участники песни.
    /// Приглашённых нет у событий старого бота и у песен без логинов — тогда берём текущий состав.
    /// </summary>
    public static IReadOnlyList<RehearsalReportRow> BuildRows(
        IEnumerable<ExternalScheduleEvent> roomEvents,
        IEnumerable<RehearsalReportSong> songs,
        IReadOnlyDictionary<DateTimeOffset, string> requesterLoginsByStart,
        ICollection<string>? unmatchedTitles = null)
    {
        var songsByKey = songs
            .GroupBy(s => Normalize($"{s.Title} — {s.Artist}"))
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<RehearsalReportRow>();
        foreach (var roomEvent in roomEvents)
        {
            var match = RehearsalTitle().Match(roomEvent.Title ?? string.Empty);
            if (!match.Success)
                continue;

            var key = Normalize(match.Groups["song"].Value);
            if (!songsByKey.TryGetValue(key, out var matched))
            {
                matched = [];
                unmatchedTitles?.Add(roomEvent.Title!);
            }

            var members = matched
                .SelectMany(s => s.MemberLogins)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var logins = new HashSet<string>(members, StringComparer.OrdinalIgnoreCase);
            if (roomEvent.Attendees is { Count: > 0 } attendees)
            {
                var notParticipants = matched
                    .Select(s => s.RoadieLogin)
                    .Append(roomEvent.OrganizerLogin)
                    .Append(requesterLoginsByStart.GetValueOrDefault(roomEvent.Start))
                    .OfType<string>()
                    .Where(login => !members.Contains(login))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                logins = attendees
                    .Where(a => !a.Declined && !notParticipants.Contains(a.Login))
                    .Select(a => a.Login)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            if (logins.Count == 0)
                rows.Add(new RehearsalReportRow(null, roomEvent.Start));
            else
                rows.AddRange(logins.Select(login => new RehearsalReportRow(login.ToLowerInvariant(), roomEvent.Start)));
        }

        return rows
            .OrderBy(r => r.Start)
            .ThenBy(r => r.Login, StringComparer.Ordinal)
            .ToList();
    }

    public static string BuildFileName(DateOnly periodStart, DateOnly periodEnd)
    {
        return $"musicclub_rehearsals_{periodStart:yyyy-MM-dd}_{periodEnd:yyyy-MM-dd}.xlsx";
    }

    public static byte[] BuildWorkbook(
        IReadOnlyList<RehearsalReportRow> rows,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        using var workbook = new XLWorkbook();
        var sheetName = Path.GetFileNameWithoutExtension(BuildFileName(periodStart, periodEnd));
        var sheet = workbook.AddWorksheet(sheetName[..Math.Min(sheetName.Length, MaxSheetNameLength)]);

        SetText(sheet.Cell(1, 1), "Club");
        SetText(sheet.Cell(1, 2), "login");
        SetText(sheet.Cell(1, 3), "datetime");

        for (var i = 0; i < rows.Count; i++)
        {
            var row = i + 2;
            SetText(sheet.Cell(row, 1), Club);
            if (rows[i].Login is { } login)
                SetText(sheet.Cell(row, 2), login);
            SetText(sheet.Cell(row, 3),
                rows[i].Start.ToOffset(Moscow).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
        }

        sheet.Columns(1, 3).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Дата — текстом, как в отчётах, которые уже принимают: Excel не должен её переформатировать.
    private static void SetText(IXLCell cell, string value)
    {
        cell.Value = XLCellValue.FromObject(value);
        cell.Style.NumberFormat.Format = "@";
    }

    private static string Normalize(string value)
    {
        return Whitespace().Replace(value, " ").Trim().TrimEnd('—').TrimEnd().ToLowerInvariant();
    }

    [GeneratedRegex(@"^\s*(?:🎸\s*)?Репетиция:?\s+(?<song>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RehearsalTitle();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
