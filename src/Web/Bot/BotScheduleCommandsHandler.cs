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
/// /status, /update, /day
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
    private static readonly Regex MentionRegex = new(@"@([A-Za-z0-9_]+)", RegexOptions.Compiled);

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
                case "day":
                    await HandleDayAsync(args, message, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling schedule command {Command}", command);
            await SendTextAsync(message, "❌ Что-то пошло не так. Попробуйте позже.");
        }
    }

    private async Task HandleStatusAsync(string? args, Message message, CancellationToken ct)
    {
        var date = ParseDate(args);
        var from = new DateTimeOffset(date.FromMskToUtc(), TimeSpan.Zero);

        var bookings = await bookingRepository.GetActiveInRangeAsync(from, from.AddDays(1), ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📅 <b>Брони на {FormatDate(date)}</b>");
        sb.AppendLine();

        if (bookings.Count == 0)
        {
            sb.AppendLine("Пока ничего — зал свободен 🎸");
            sb.AppendLine("Подробнее с календарями участников — /day");
            await SendTextAsync(message, sb.ToString());
            return;
        }

        foreach (var booking in bookings)
        {
            var song = booking.SongId is { } songId ? await songRepository.FindByIdAsync(songId, ct) : null;
            var icon = booking.Status == BookingStatus.Pending ? "⏳" : "🎸";
            var title = song == null ? "Репетиция" : $"{Html(song.Title)} — {Html(song.Artist)}";
            var suffix = booking.Status == BookingStatus.Pending ? " <i>(ждёт подтверждения)</i>" : "";
            sb.AppendLine($"{icon} <code>{Range(booking.ScheduledAt, booking.ScheduledAt.AddMinutes(booking.DurationMinutes))}</code> {title}{suffix}");
        }

        await SendTextAsync(message, sb.ToString());
    }

    private async Task HandleUpdateAsync(string? args, Message message, User user, CancellationToken ct)
    {
        if (!await bookingService.CanApproveAsync(user.Id, ct))
        {
            await SendTextAsync(message, "⛔ Публиковать сводку могут только организаторы.");
            return;
        }

        var chatId = message.Chat.Id;
        var topicId = message.MessageThreadId;

        var result = await digestService.PublishDigestAsync(
            chatId,
            topicId,
            BotDateParser.NowMsk().Date,
            ct);

        if (result.Success)
        {
            await SendTextAsync(message, "✅ Сводка на неделю опубликована.");
        }
        else
        {
            await SendTextAsync(message, $"❌ Не удалось опубликовать сводку: {Html(result.Message)}");
        }
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
        return BotDateParser.FormatDate(date);
    }

    private static string Html(string? text)
    {
        return WebUtility.HtmlEncode(text ?? "");
    }

    private static DateTime ParseDate(string? args)
    {
        // Kind=Unspecified, время МСК: репозиторий переводит его в UTC как МСК
        var today = BotDateParser.NowMsk().Date;
        var token = args?.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return BotDateParser.TryParseDate(token, today, out var date) ? date : today;
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
