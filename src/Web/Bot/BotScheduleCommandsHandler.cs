using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Обработчик команд отображения расписания:
/// /status, /update, /recheck, /day, /info, /help
/// </summary>
public class BotScheduleCommandsHandler(
    IRehearsalBookingService bookingService,
    IWeeklyDigestService digestService,
    ISongRepository songRepository,
    ISongTopicRepository songTopicRepository,
    ITelegramChatService telegramChatService,
    ITelegramBotClient botClient,
    IRehearsalBookingRepository bookingRepository,
    IDayScheduleService dayScheduleService,
    ILogger<BotScheduleCommandsHandler> logger)
{
    private static readonly Regex DateRegex = new(@"^(\d{1,2})[./-](\d{1,2})$", RegexOptions.Compiled);
    private static readonly Regex MentionRegex = new(@"@([A-Za-z0-9_]+)", RegexOptions.Compiled);
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Dictionary<string, int> DayAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["пн"] = 1, ["mon"] = 1,
        ["вт"] = 2, ["tue"] = 2,
        ["ср"] = 3, ["wed"] = 3,
        ["чт"] = 4, ["thu"] = 4,
        ["пт"] = 5, ["fri"] = 5,
        ["сб"] = 6, ["sat"] = 6,
        ["вс"] = 7, ["sun"] = 7,
    };

    public async Task HandleScheduleCommandAsync(
        string command,
        string? args,
        Message message,
        User user,
        CancellationToken ct)
    {
        try
        {
            switch (command.ToLowerInvariant())
            {
                case "status":
                    await HandleStatusAsync(args, message, ct);
                    break;
                case "update":
                    await HandleUpdateAsync(args, message, user, ct);
                    break;
                case "recheck":
                    await HandleRecheckAsync(args, message, user, ct);
                    break;
                case "day":
                    await HandleDayAsync(args, message, ct);
                    break;
                case "info":
                case "help":
                    await HandleInfoAsync(message, user, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling schedule command {Command}", command);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Произошла ошибка. Попробуйте позже.",
                cancellationToken: ct);
        }
    }

    private async Task HandleStatusAsync(string? args, Message message, CancellationToken ct)
    {
        var date = ParseDate(args);

        var bookings = await bookingRepository.GetUpcomingAsync(
            date.Date,
            date.Date.AddDays(1),
            ct);

        if (bookings.Count == 0)
        {
            await SendTextAsync(message, $"📅 Расписание на {FormatDate(date)}:\n\nНичего нет. 🎸");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📅 Расписание на {FormatDate(date)}");
        sb.AppendLine();

        foreach (var booking in bookings.OrderBy(b => b.ScheduledAt))
        {
            var time = booking.ScheduledAt.ToOffset(TimeSpan.FromHours(3)).ToString("HH:mm");
            var end = booking.ScheduledAt.ToOffset(TimeSpan.FromHours(3)).AddMinutes(booking.DurationMinutes).ToString("HH:mm");
            sb.AppendLine($"⏰ <code>{time}–{end}</code>");
        }

        await SendTextAsync(message, sb.ToString());
    }

    private async Task HandleUpdateAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var topicId = message.MessageThreadId;

        var result = await digestService.PublishDigestAsync(
            chatId,
            topicId,
            DateTime.Today,
            ct);

        if (result.Success)
        {
            await SendTextAsync(message, "✅ Еженедельная сводка обновлена.");
        }
        else
        {
            await SendTextAsync(message, $"❌ Ошибка: {result.Message}");
        }
    }

    private async Task HandleRecheckAsync(string? args, Message message, User user, CancellationToken ct)
    {
        if (!message.Chat.IsDirectMessages)
        {
            await SendTextAsync(message, "❌ /recheck можно использовать только в личных сообщениях.");
            return;
        }

        await SendTextAsync(message, "🔄 Обновление сводки...");
        // TODO: реализовать обновление сохранённой недели
    }

    private async Task HandleDayAsync(string? args, Message message, CancellationToken ct)
    {
        // «/day 08.10 без @user1 @user2» — как в musicscheduler
        var dateArgs = args;
        var excluded = new List<string>();
        var withoutIndex = args?.IndexOf("без", StringComparison.OrdinalIgnoreCase) ?? -1;
        if (withoutIndex >= 0)
        {
            dateArgs = args![..withoutIndex];
            excluded.AddRange(MentionRegex.Matches(args[withoutIndex..]).Select(m => m.Groups[1].Value));
        }

        var date = ParseDate(dateArgs);
        var topic = await ResolveTopicAsync(message, ct);

        var status = await botClient.SendMessage(
            message.Chat.Id,
            $"⏳ Собираю расписание на {FormatDate(date)}...",
            messageThreadId: message.MessageThreadId,
            cancellationToken: ct);

        var schedule = await dayScheduleService.GetDayAsync(
            DateOnly.FromDateTime(date), topic?.SongId, excluded, ct);

        byte[] png;
        try
        {
            png = DayScheduleImageRenderer.Render(schedule);
        }
        catch (Exception ex)
        {
            // Без картинки — хотя бы текстом
            logger.LogError(ex, "Не удалось отрисовать расписание на {Date}", date);
            await botClient.EditMessageText(
                message.Chat.Id,
                status.MessageId,
                FormatDaySchedule(schedule, topic, excluded),
                parseMode: ParseMode.Html,
                cancellationToken: CancellationToken.None);
            return;
        }

        var caption = new StringBuilder($"🗓 <b>Расписание на {FormatDate(date)}</b>");
        if (excluded.Count > 0)
            caption.Append($"\n<i>Без учёта: {Html(string.Join(", ", excluded.Select(u => "@" + u)))}</i>");
        if (!schedule.RoomKnown)
            caption.Append("\n⚠️ Расписание зала недоступно");
        if (topic == null)
            caption.Append("\n<i>Вызовите /day в топике песни, чтобы добавить участников.</i>");

        using var stream = new MemoryStream(png);
        await botClient.SendPhoto(
            message.Chat.Id,
            InputFile.FromStream(stream, $"schedule_{date:dd_MM}.png"),
            caption: caption.ToString(),
            parseMode: ParseMode.Html,
            messageThreadId: message.MessageThreadId,
            replyParameters: new ReplyParameters { MessageId = message.MessageId },
            cancellationToken: CancellationToken.None);
        await botClient.DeleteMessage(message.Chat.Id, status.MessageId, CancellationToken.None);
    }

    private static string FormatDaySchedule(DaySchedule schedule, SongTopic? topic, IReadOnlyCollection<string> excluded)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📊 <b>Расписание на {FormatDate(schedule.Date.ToDateTime())}</b>");
        if (topic != null)
            sb.AppendLine($"🎵 {Html(topic.Title)}");
        if (excluded.Count > 0)
            sb.AppendLine($"<i>Без учёта: {Html(string.Join(", ", excluded.Select(u => "@" + u)))}</i>");
        sb.AppendLine();

        sb.AppendLine("🏠 <b>Зал:</b>");
        if (!schedule.RoomKnown)
            sb.AppendLine("⚠️ Расписание зала недоступно (не задан YandexCalendar__RoomIcsToken или ошибка загрузки).");
        foreach (var item in schedule.Room)
        {
            var icon = item.Status switch
            {
                RoomSlotStatus.Busy => "⛔",
                RoomSlotStatus.Coach => "🟡",
                _ => "✅"
            };
            sb.AppendLine($"{icon} <code>{Range(item.Start, item.End)}</code> {Html(item.Title)}");
        }

        var pending = schedule.Bookings.Where(b => b.Status == BookingStatus.Pending).ToList();
        foreach (var b in pending)
            sb.AppendLine($"⏳ <code>{Range(b.ScheduledAt, b.ScheduledAt.AddMinutes(b.DurationMinutes))}</code> ждёт подтверждения Ильи");

        if (topic == null)
        {
            sb.AppendLine();
            sb.AppendLine("<i>Вызовите /day в топике песни, чтобы сопоставить с расписанием участников.</i>");
            return sb.ToString();
        }

        sb.AppendLine();
        sb.AppendLine("👥 <b>Участники:</b>");
        if (schedule.Members.Count == 0)
            sb.AppendLine("Нет участников с назначенными ролями.");
        foreach (var member in schedule.Members)
        {
            var name = Html(member.Name);
            var line = member.Status switch
            {
                MemberScheduleStatus.NoYandexLogin => $"❓ {name} — нет Яндекс-логина",
                MemberScheduleStatus.Error => $"⚠️ {name} — не удалось получить календарь",
                _ when member.Events.Count == 0 => $"🟢 {name} — свободен весь день",
                _ => $"🔴 {name}: " + string.Join(", ", member.Events.Select(e => $"<code>{Range(e.Start, e.End)}</code>"))
            };
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine($"✨ <b>Общие окна ({DayScheduleService.WorkStartHour}:00–{DayScheduleService.WorkEndHour}:00, от часа):</b>");
        if (schedule.FreeWindows.Count == 0)
            sb.AppendLine("Нет подходящих окон.");
        foreach (var window in schedule.FreeWindows)
            sb.AppendLine($"{(window.WithCoach ? "🟡" : "✅")} <code>{Range(window.Start, window.End)}</code>"
                          + (window.WithCoach ? " с Ильёй" : ""));

        if (schedule.Members.Any(m => m.Status != MemberScheduleStatus.Ok))
            sb.AppendLine("<i>Участники без календаря в окнах не учтены.</i>");

        return sb.ToString();
    }

    private async Task<SongTopic?> ResolveTopicAsync(Message message, CancellationToken ct)
    {
        if (!message.IsTopicMessage || message.MessageThreadId == null)
            return null;

        return await songTopicRepository.FindByTopicIdAsync((long) message.MessageThreadId, ct);
    }

    private static string Range(DateTimeOffset start, DateTimeOffset end)
    {
        return $"{start.UtcDateTime.FromUtcToMsk():HH:mm}–{end.UtcDateTime.FromUtcToMsk():HH:mm}";
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("dd.MM, ddd", Ru);
    }

    private static string Html(string? text)
    {
        return WebUtility.HtmlEncode(text ?? "");
    }

    private async Task HandleInfoAsync(Message message, User user, CancellationToken ct)
    {
        var lang = user.LanguageCode;
        var isRu = lang != null && lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        var helpText = isRu
            ? @"📋 <b>Доступные команды:</b>

<b>Бронирование:</b>
/check ДД.MM ЧЧ:ММ — проверить доступность слота
/slots — найти свободные слоты на неделю
/slots_with — то же + проверить Илью
/take ДД.MM ЧЧ:ММ — забронировать слот
/take_with ДД.MM ЧЧ:ММ — забронировать с запросом к Илье
/approve — подтвердить бронирование (Илья)
/reject — отклонить бронирование (Илья)
/cancel ДД.MM ЧЧ:ММ — отменить бронирование

<b>Расписание:</b>
/status [ДД.MM] — расписание на день
/update — опубликовать еженедельную сводку (Илья)
/recheck — обновить сводку (Илья, ЛС)
/day [ДД.MM] — расписание на день

<b>Другое:</b>
/info — эта справка"
            : @"📋 <b>Available commands:</b>

<b>Booking:</b>
/check DD.MM HH:MM — check slot availability
/slots — find free slots this week
/slots_with — same + check coach
/take DD.MM HH:MM — book a slot
/take_with DD.MM HH:MM — book with coach approval
/approve — approve a booking (coach)
/reject — reject a booking (coach)
/cancel DD.MM HH:MM — cancel a booking

<b>Schedule:</b>
/status [DD.MM] — schedule for the day
/update — publish weekly digest (coach)
/recheck — refresh digest (coach, DM)
/day [DD.MM] — daily schedule

<b>Other:</b>
/info — this help message";

        await botClient.SendMessage(
            message.Chat.Id,
            helpText,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private DateTime ParseDate(string? args)
    {
        // Kind=Unspecified, время МСК: репозиторий переводит его в UTC как МСК
        var today = DateTime.UtcNow.FromUtcToMsk().Date;

        if (string.IsNullOrWhiteSpace(args))
            return today;

        if (DateRegex.Match(args.Trim()) is { Success: true } dateMatch)
        {
            var day = int.Parse(dateMatch.Groups[1].Value);
            var month = int.Parse(dateMatch.Groups[2].Value);
            return new DateTime(today.Year, month, day);
        }

        if (DayAliases.TryGetValue(args.Trim(), out var dow))
        {
            var todayDow = (int) today.DayOfWeek;
            todayDow = todayDow == 0 ? 7 : todayDow;
            var diff = dow - todayDow;
            if (diff <= 0) diff += 7;
            return today.AddDays(diff);
        }

        return today;
    }

    private Task SendTextAsync(Message message, string text)
    {
        return botClient.SendMessage(
            message.Chat.Id,
            text,
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            cancellationToken: CancellationToken.None);
    }
}
