using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Application.Services.Calendar;
using SkiaSharp;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Картинка расписания дня для /day: Время | Зал | Участник 1 | … | Занятость.
/// Порт generate_day_schedule_image из musicscheduler/main_user.py (те же размеры, цвета и правила).
/// </summary>
public static class DayScheduleImageRenderer
{
    private const int StartHour = DayScheduleService.WorkStartHour;
    private const int EndHour = DayScheduleService.WorkEndHour;

    private const int TimeColW = 60;
    private const int RoomColW = 160;
    private const int UserColW = 140;
    private const int SummaryColW = 150;
    private const int HeaderH = 56;
    private const int PxPerHour = 80;

    private static readonly SKColor BgColor = new(30, 30, 32);
    private static readonly SKColor GridColor = new(60, 60, 65);
    private static readonly SKColor TextColor = new(230, 230, 230);

    private static readonly (byte R, byte G, byte B) SlotColor = (90, 180, 110);
    private static readonly (byte R, byte G, byte B) RoomBusyColor = (210, 120, 80);
    private static readonly (byte R, byte G, byte B) UserColor = (100, 140, 220);
    private static readonly (byte R, byte G, byte B) SummaryBusyColor = (200, 70, 70);
    private static readonly (byte R, byte G, byte B) SummaryFreeColor = (70, 180, 70);
    private static readonly (byte R, byte G, byte B) SummaryOverlapColor = (200, 160, 50);

    private static readonly TimeSpan MinSummaryBlock = TimeSpan.FromMinutes(60);
    private static readonly string[] BusyNames = ["BUSY", "СОБЫТИЕ СКРЫТО"];

    private static readonly Lazy<SKTypeface> Typeface = new(LoadTypeface);

    public static byte[] Render(DaySchedule schedule)
    {
        var columns = schedule.Members.Select(m => (Header: MemberHeader(m), m.Events)).ToList();

        var width = TimeColW + RoomColW + columns.Count * UserColW + SummaryColW;
        var height = HeaderH + (EndHour - StartHour) * PxPerHour;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(BgColor);

        using var font = CreateFont(13);
        using var fontHeader = CreateFont(14);
        using var fontTime = CreateFont(12);
        using var gridPaint = new SKPaint { Color = GridColor, StrokeWidth = 1, IsAntialias = false };
        using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };

        // Сетка и заголовки
        var headers = new List<string> { "Зал" };
        headers.AddRange(columns.Select(c => c.Header));
        headers.Add("Занятость");
        var widths = new List<int> { RoomColW };
        widths.AddRange(columns.Select(_ => UserColW));
        widths.Add(SummaryColW);

        var x = TimeColW;
        for (var i = 0; i < headers.Count; i++)
        {
            canvas.DrawLine(x + 0.5f, 0, x + 0.5f, height, gridPaint);

            var text = headers[i];
            if (text.Length > 10 && text.Contains(' ') && !text.Contains('\n'))
                text = ReplaceFirst(text, " ", "\n");

            DrawCenteredMultiline(canvas, text, x, widths[i], fontHeader, textPaint);
            x += widths[i];
        }

        for (var h = StartHour; h <= EndHour; h++)
        {
            var y = HeaderH + (h - StartHour) * PxPerHour;
            canvas.DrawLine(0, y + 0.5f, width, y + 0.5f, gridPaint);
            if (h < EndHour)
                DrawTextTop(canvas, $"{h}:00", 12, y + 6, font, textPaint);
        }

        var dayStart = MskToUtc(schedule.Date, StartHour);
        var dayEnd = MskToUtc(schedule.Date, EndHour);

        // Зал: свободное время не рисуем, занятое — с красной полоской BUSY
        var roomEvents = schedule.Room.Where(r => r.Status != RoomSlotStatus.Free).ToList();
        foreach (var e in roomEvents)
        {
            var isSlot = e.Status == RoomSlotStatus.Coach;
            DrawEvent(canvas, TimeColW, RoomColW, e.Start, e.End, e.Title, isSlot ? SlotColor : RoomBusyColor,
                e.Status == RoomSlotStatus.Busy, dayStart, dayEnd, font, fontTime);
        }

        var currentX = TimeColW + RoomColW;
        foreach (var column in columns)
        {
            foreach (var e in column.Events)
            {
                var name = e.Title?.Trim() ?? "";
                var isBusy = BusyNames.Contains(name.ToUpperInvariant());
                DrawEvent(canvas, currentX, UserColW, e.Start, e.End, name, UserColor, isBusy, dayStart, dayEnd,
                    font, fontTime);
            }

            currentX += UserColW;
        }

        // Колонка «Занятость»
        var summaryX = width - SummaryColW;
        foreach (var block in BuildSummary(roomEvents, columns.Select(c => c.Events).ToList(), dayStart, dayEnd))
        {
            if (block.End - block.Start < MinSummaryBlock)
                continue;
            DrawEvent(canvas, summaryX, SummaryColW, block.Start, block.End, block.Text, block.Color, false,
                dayStart, dayEnd, font, fontTime);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string MemberHeader(MemberSchedule member)
    {
        return member.Status switch
        {
            MemberScheduleStatus.NoYandexLogin => $"{member.Name}\n(нет email)",
            MemberScheduleStatus.Error => $"{member.Name}\n(ошибка)",
            _ => member.Name
        };
    }

    private static List<(DateTimeOffset Start, DateTimeOffset End, string Text, (byte R, byte G, byte B) Color)>
        BuildSummary(
            IReadOnlyCollection<RoomTimelineItem> roomEvents,
            IReadOnlyCollection<IReadOnlyList<ExternalScheduleEvent>> usersEvents,
            DateTimeOffset dayStart,
            DateTimeOffset dayEnd)
    {
        DateTimeOffset Clamp(DateTimeOffset t)
        {
            return t < dayStart ? dayStart : t > dayEnd ? dayEnd : t;
        }

        var boundaries = new HashSet<DateTimeOffset> { dayStart, dayEnd };
        foreach (var e in roomEvents)
        {
            boundaries.Add(Clamp(e.Start));
            boundaries.Add(Clamp(e.End));
        }

        foreach (var e in usersEvents.SelectMany(u => u))
        {
            boundaries.Add(Clamp(e.Start));
            boundaries.Add(Clamp(e.End));
        }

        var sorted = boundaries.OrderBy(b => b).ToList();
        var blocks = new List<(DateTimeOffset Start, DateTimeOffset End, string Text, (byte R, byte G, byte B) Color)>();

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var mid = sorted[i] + (sorted[i + 1] - sorted[i]) / 2;

            var roomBusy = roomEvents.Any(r => r.Start <= mid && mid < r.End && r.Status != RoomSlotStatus.Coach);
            var busyCount = usersEvents.Count(events => events.Any(e => e.Start <= mid && mid < e.End));

            var (text, color) = roomBusy
                ? ("Зал занят", SummaryBusyColor)
                : busyCount == 0
                    ? ("Свободно", SummaryFreeColor)
                    : ($"Пересеч: {busyCount}", SummaryOverlapColor);

            if (blocks.Count > 0 && blocks[^1].Text == text)
                blocks[^1] = blocks[^1] with { End = sorted[i + 1] };
            else
                blocks.Add((sorted[i], sorted[i + 1], text, color));
        }

        return blocks;
    }

    private static void DrawEvent(
        SKCanvas canvas,
        int colX,
        int colW,
        DateTimeOffset start,
        DateTimeOffset end,
        string name,
        (byte R, byte G, byte B) baseColor,
        bool isBusy,
        DateTimeOffset dayStart,
        DateTimeOffset dayEnd,
        SKFont font,
        SKFont fontTime)
    {
        var evStart = start < dayStart ? dayStart : start;
        var evEnd = end > dayEnd ? dayEnd : end;
        if (evStart >= evEnd)
            return;

        var startMsk = evStart.UtcDateTime.FromUtcToMsk();
        var endMsk = evEnd.UtcDateTime.FromUtcToMsk();

        var y1 = HeaderH + (float) ((startMsk - startMsk.Date).TotalHours - StartHour) * PxPerHour;
        var y2 = HeaderH + (float) ((endMsk - startMsk.Date).TotalHours - StartHour) * PxPerHour;
        if (y2 - y1 < 2)
            return;

        var rect = new SKRect(colX + 3, y1 + 2, colX + colW - 3, y2 - 2);

        using (var fill = new SKPaint { Color = new SKColor(baseColor.R, baseColor.G, baseColor.B, 70), IsAntialias = true })
            canvas.DrawRoundRect(rect, 8, 8, fill);
        using (var outline = new SKPaint
               {
                   Color = new SKColor(baseColor.R, baseColor.G, baseColor.B, 180),
                   Style = SKPaintStyle.Stroke,
                   StrokeWidth = 1,
                   IsAntialias = true
               })
            canvas.DrawRoundRect(rect, 8, 8, outline);

        var textOffsetX = 10;

        // Индикатор BUSY — красная вертикальная полоска слева
        if (isBusy)
        {
            using var stripe = new SKPaint { Color = new SKColor(235, 70, 70), IsAntialias = true };
            canvas.DrawRoundRect(new SKRect(colX + 3, y1 + 2, colX + 7, y2 - 2), 3, 3, stripe);
            textOffsetX = 14;
        }

        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var nameColor = new SKPaint { Color = new SKColor(235, 235, 235), IsAntialias = true };

        var timeStr = $"{startMsk:HH:mm} - {endMsk:HH:mm}";
        var ty = y1 + 6;

        if (ty + 12 <= y2 - 4)
        {
            DrawTextTop(canvas, timeStr, colX + textOffsetX, ty, fontTime, white);

            if (isBusy)
            {
                var timeW = fontTime.MeasureText(timeStr);
                using var busyPaint = new SKPaint { Color = new SKColor(255, 120, 120), IsAntialias = true };
                DrawTextTop(canvas, "BUSY", colX + textOffsetX + timeW + 6, ty, fontTime, busyPaint);
            }

            ty += 16;
        }

        foreach (var line in Wrap(name, (int) ((colW - textOffsetX) / 7.5)))
        {
            if (ty + 12 > y2 - 4)
                break;
            DrawTextTop(canvas, line, colX + textOffsetX, ty, font, nameColor);
            ty += 15;
        }
    }

    private static void DrawCenteredMultiline(SKCanvas canvas, string text, int colX, int colW, SKFont font, SKPaint paint)
    {
        var lines = text.Split('\n');
        var lineH = font.Spacing;
        var totalH = lineH * lines.Length;
        var top = (HeaderH - totalH) / 2;

        for (var i = 0; i < lines.Length; i++)
        {
            var w = font.MeasureText(lines[i]);
            DrawTextTop(canvas, lines[i], colX + (colW - w) / 2, top + i * lineH, font, paint);
        }
    }

    /// <summary>
    /// Как PIL draw.text: (x, y) — верхний левый угол строки, а не базовая линия.
    /// </summary>
    private static void DrawTextTop(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        // Контуром, а не DrawText: у глифов Skia своя гамма/контраст и текст выходит заметно жирнее, чем в Pillow
        using var path = font.GetTextPath(text, new SKPoint(x, MathF.Round(y - font.Metrics.Ascent) + 1));
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Упрощённый textwrap.wrap: перенос по словам, длинные слова режутся по ширине.
    /// </summary>
    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        if (width <= 0 || string.IsNullOrWhiteSpace(text))
            return lines;

        var current = "";
        foreach (var word in text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = word;
            while (rest.Length > 0)
            {
                var candidate = current.Length == 0 ? rest : current + " " + rest;
                if (candidate.Length <= width)
                {
                    current = candidate;
                    rest = "";
                }
                else if (current.Length > 0)
                {
                    lines.Add(current);
                    current = "";
                }
                else
                {
                    lines.Add(rest[..width]);
                    rest = rest[width..];
                }
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    private static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + replacement + text[(index + search.Length)..];
    }

    private static DateTimeOffset MskToUtc(DateOnly date, int hour)
    {
        return new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, 0)).FromMskToUtc(), TimeSpan.Zero);
    }

    private static SKFont CreateFont(float size)
    {
        // Ближе к FreeType в Pillow: обычное сглаживание с хинтингом, без субпикселей
        return new SKFont(Typeface.Value, size)
        {
            Edging = SKFontEdging.Antialias,
            Hinting = SKFontHinting.Full,
            Subpixel = false
        };
    }

    private static SKTypeface LoadTypeface()
    {
        using var stream = typeof(DayScheduleImageRenderer).Assembly.GetManifestResourceStream("DejaVuSans.ttf")
                           ?? throw new InvalidOperationException("Не найден встроенный шрифт DejaVuSans.ttf");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var data = SKData.CreateCopy(buffer.ToArray());
        return SKTypeface.FromData(data) ?? SKTypeface.Default;
    }
}
