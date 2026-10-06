using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Обработчик команд бронирования репетиций:
/// /check, /slots, /slots_with, /take, /take_with, /approve, /reject, /cancel.
/// Свободные окна и проверка слота считаются через <see cref="IDayScheduleService"/> — так же, как в /day.
/// </summary>
public class BotRehearsalCommandsHandler(
    IRehearsalBookingService bookingService,
    ISongRepository songRepository,
    ISongTopicRepository songTopicRepository,
    IDayScheduleService dayScheduleService,
    ITelegramBotClient botClient,
    IRehearsalBookingRepository bookingRepository,
    IApplicationUserRepository userRepository,
    ILogger<BotRehearsalCommandsHandler> logger)
{
    public const string CallbackPrefix = "booking:";

    private const int DefaultDurationMinutes = 80;
    private const int MinSlotMinutes = 60;
    private const int SlotsDays = 7;

    private static readonly Regex MentionRegex = new(@"@?([A-Za-z0-9_]{3,})", RegexOptions.Compiled);

    public async Task HandleRehearsalCommandAsync(
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
                case "check":
                    await HandleCheckAsync(args, message, user, ct);
                    break;
                case "slots":
                    await HandleSlotsAsync(args, message, user, ct, withCoach: false);
                    break;
                case "slots_with":
                    await HandleSlotsAsync(args, message, user, ct, withCoach: true);
                    break;
                case "take":
                    await HandleTakeAsync(args, message, user, ct);
                    break;
                case "take_with":
                    await HandleTakeWithAsync(args, message, user, ct);
                    break;
                case "approve":
                    await HandleApproveOrRejectAsync(args, message, user, approve: true, ct);
                    break;
                case "reject":
                    await HandleApproveOrRejectAsync(args, message, user, approve: false, ct);
                    break;
                case "cancel":
                    await HandleCancelAsync(args, message, user, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling rehearsal command {Command}", command);
            await SendTextAsync(message, "❌ Что-то пошло не так. Попробуйте позже.");
        }
    }

    private async Task HandleCheckAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var song = await ResolveSongAsync(message, "/check", ct);
        if (song == null) return;

        if (!BotDateParser.TryParseDateTime(args, BotDateParser.NowMsk(), out var start))
        {
            await SendTextAsync(message,
                "📋 Формат: <code>/check ДАТА ВРЕМЯ</code>\n" +
                $"Дата — {BotDateParser.DateHint}.\n" +
                "Примеры: <code>/check 19.02 18:00</code>, <code>/check пт 17</code>");
            return;
        }

        var startUtc = ToUtcOffset(start);
        var endUtc = startUtc.AddMinutes(DefaultDurationMinutes);
        var schedule = await dayScheduleService.GetDayAsync(DateOnly.FromDateTime(start), song.Id, [], ct);

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 <b>{BotDateParser.FormatDate(start)}, {Range(startUtc, endUtc)}</b>");
        sb.AppendLine($"🎵 {Html(song.Title)}");
        sb.AppendLine();

        var problems = 0;

        if (startUtc < DateTimeOffset.UtcNow)
        {
            sb.AppendLine("⚠️ Это время уже прошло.");
            problems++;
        }

        if (start.Hour < DayScheduleService.WorkStartHour ||
            start.AddMinutes(DefaultDurationMinutes) >
            start.Date.AddHours(DayScheduleService.WorkEndHour))
        {
            sb.AppendLine(
                $"⚠️ Выходит за рабочие часы зала ({DayScheduleService.WorkStartHour}:00–{DayScheduleService.WorkEndHour}:00).");
            problems++;
        }

        var roomBusy = schedule.Room
            .Where(r => r.Status == RoomSlotStatus.Busy && r.Start < endUtc && r.End > startUtc)
            .ToList();
        var coachSlot = schedule.Room
            .Any(r => r.Status == RoomSlotStatus.Coach && r.Start < endUtc && r.End > startUtc);

        if (!schedule.RoomKnown)
            sb.AppendLine("🏠 Зал: ❔ расписание зала недоступно");
        else if (roomBusy.Count > 0)
        {
            sb.AppendLine("🏠 Зал: ⛔ занят — " + string.Join(", ",
                roomBusy.Select(r => $"{Html(r.Title)} <code>{Range(r.Start, r.End)}</code>")));
            problems++;
        }
        else
            sb.AppendLine(coachSlot ? "🏠 Зал: 🟡 свободен, слот Ильи" : "🏠 Зал: ✅ свободен");

        var busyMembers = schedule.Members
            .Where(m => m.Events.Any(e => e.Start < endUtc && e.End > startUtc))
            .Select(m => Html(m.Name))
            .ToList();
        var unknownMembers = schedule.Members
            .Where(m => m.Status != MemberScheduleStatus.Ok)
            .Select(m => Html(m.Name))
            .ToList();

        if (schedule.Members.Count == 0)
            sb.AppendLine("👥 Участники: у песни нет назначенных ролей");
        else if (busyMembers.Count > 0)
        {
            sb.AppendLine("👥 Заняты: " + string.Join(", ", busyMembers));
            problems++;
        }
        else
            sb.AppendLine("👥 Участники: ✅ все свободны");

        if (unknownMembers.Count > 0)
            sb.AppendLine("❔ Не проверены (нет календаря): " + string.Join(", ", unknownMembers));

        sb.AppendLine();
        sb.AppendLine(problems == 0
            ? $"👍 Можно бронировать: <code>/take {start:dd.MM HH:mm}</code>"
            : "👎 Лучше выбрать другое время — см. /slots");

        await SendTextAsync(message, sb.ToString());
    }

    private async Task HandleSlotsAsync(string? args, Message message, User user, CancellationToken ct, bool withCoach)
    {
        var command = withCoach ? "/slots_with" : "/slots";
        var song = await ResolveSongAsync(message, command, ct);
        if (song == null) return;

        var excluded = ParseExcludedUsers(args);
        var now = DateTimeOffset.UtcNow;
        var today = BotDateParser.NowMsk().Date;

        var status = await botClient.SendMessage(
            message.Chat.Id,
            "⏳ Ищу свободные окна на неделю…",
            messageThreadId: message.MessageThreadId,
            cancellationToken: ct);

        var days = new List<(DateTime Date, List<FreeWindow> Windows)>();
        var roomKnown = true;
        var unknownMembers = new SortedSet<string>(StringComparer.CurrentCulture);
        var membersCount = 0;

        for (var i = 0; i < SlotsDays; i++)
        {
            var date = today.AddDays(i);
            var schedule = await dayScheduleService.GetDayAsync(DateOnly.FromDateTime(date), song.Id, excluded, ct);

            roomKnown &= schedule.RoomKnown;
            membersCount = Math.Max(membersCount, schedule.Members.Count);
            foreach (var member in schedule.Members.Where(m => m.Status != MemberScheduleStatus.Ok))
                unknownMembers.Add(member.Name);

            var windows = schedule.FreeWindows
                .Where(w => !withCoach || w.WithCoach)
                // Сегодня показываем только то, что ещё не началось
                .Select(w => w.Start < now ? w with { Start = RoundUpToQuarter(now) } : w)
                .Where(w => w.End - w.Start >= TimeSpan.FromMinutes(MinSlotMinutes))
                .ToList();

            if (windows.Count > 0)
                days.Add((date, windows));
        }

        var sb = new StringBuilder();
        sb.AppendLine(withCoach
            ? "📅 <b>Свободные окна с Ильёй на 7 дней</b>"
            : "📅 <b>Свободные окна на 7 дней</b>");
        sb.AppendLine($"🎵 {Html(song.Title)}");
        if (excluded.Count > 0)
            sb.AppendLine($"<i>Без учёта: {Html(string.Join(", ", excluded.Select(u => "@" + u)))}</i>");
        sb.AppendLine();

        if (days.Count == 0)
        {
            sb.AppendLine(withCoach
                ? "Окон со слотами Ильи не нашлось. Попробуйте /slots — без тренера."
                : "Общих свободных окон не нашлось. Попробуйте исключить кого-то: <code>/slots без @username</code>");
        }
        else
        {
            foreach (var (date, windows) in days)
            {
                sb.AppendLine($"<b>{BotDateParser.FormatDayHeader(date)}</b>");
                foreach (var w in windows)
                    sb.AppendLine($"{(w.WithCoach ? "🟡" : "✅")} <code>{Range(w.Start, w.End)}</code>"
                                  + (w.WithCoach ? " с Ильёй" : ""));
            }

            sb.AppendLine();
            sb.AppendLine("Забронировать: <code>/take ДД.ММ ЧЧ:ММ</code>");
        }

        if (!roomKnown)
            sb.AppendLine("⚠️ Расписание зала недоступно — занятость зала не учтена.");
        if (membersCount == 0)
            sb.AppendLine("⚠️ У песни нет участников с ролями — учтён только зал.");
        if (unknownMembers.Count > 0)
            sb.AppendLine($"❔ Не учтены (нет календаря): {Html(string.Join(", ", unknownMembers))}");

        await botClient.EditMessageText(
            message.Chat.Id,
            status.MessageId,
            sb.ToString(),
            parseMode: ParseMode.Html,
            cancellationToken: CancellationToken.None);
    }

    private async Task HandleTakeAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var song = await ResolveSongAsync(message, "/take", ct);
        if (song == null) return;

        if (!BotDateParser.TryParseDateTime(args, BotDateParser.NowMsk(), out var scheduledAt))
        {
            await SendUsageAsync(message, "/take");
            return;
        }

        RehearsalBooking booking;
        try
        {
            booking = await bookingService.CreateBookingAsync(user.Id, scheduledAt, song.Id, ct: ct);
        }
        catch (BookingRuleException ex)
        {
            await SendTextAsync(message, $"❌ Не получилось забронировать: {Html(ex.Message)}");
            return;
        }
        catch (ForbiddenAccessException)
        {
            await SendTextAsync(message, "⛔ У вас нет прав бронировать зал.");
            return;
        }

        await SendTextAsync(message,
            $"✅ <b>Зал забронирован</b>\n" +
            $"📅 {FormatBookingRange(booking)}\n" +
            $"🎵 {Html(song.Title)} — {Html(song.Artist)}\n" +
            $"👤 {Mention(user)}\n\n" +
            $"Отменить: <code>/cancel {FormatMsk(booking.ScheduledAt)}</code>");
    }

    private async Task HandleTakeWithAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var song = await ResolveSongAsync(message, "/take_with", ct);
        if (song == null) return;

        if (!BotDateParser.TryParseDateTime(args, BotDateParser.NowMsk(), out var scheduledAt))
        {
            await SendUsageAsync(message, "/take_with");
            return;
        }

        RehearsalBooking booking;
        try
        {
            booking = await bookingService.CreateBookingWithCoachAsync(user.Id, scheduledAt, song.Id, ct: ct);
        }
        catch (BookingRuleException ex)
        {
            await SendTextAsync(message, $"❌ Не получилось отправить заявку: {Html(ex.Message)}");
            return;
        }
        catch (ForbiddenAccessException)
        {
            await SendTextAsync(message, "⛔ У вас нет прав бронировать зал.");
            return;
        }

        // Скрытые упоминания — чтобы организаторам пришло уведомление
        var approvers = await userRepository.GetUsersByPermissionAsync(Permission.EventsEdit, ct);
        var pings = string.Concat(approvers
            .Where(a => a.TgUserId is not null && a.TgUserId != user.Id)
            .Select(a => $"<a href=\"tg://user?id={a.TgUserId}\">\u2060</a>"));

        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"{CallbackPrefix}approve:{booking.Id:N}"),
                InlineKeyboardButton.WithCallbackData("❌ Отклонить", $"{CallbackPrefix}reject:{booking.Id:N}"),
            ],
        ]);

        await botClient.SendMessage(
            message.Chat.Id,
            $"📝 <b>Заявка на репетицию с Ильёй</b>\n" +
            $"📅 {FormatBookingRange(booking)}\n" +
            $"🎵 {Html(song.Title)} — {Html(song.Artist)}\n" +
            $"👤 {Mention(user)}\n\n" +
            $"⏳ Ждёт подтверждения организатора.{pings}",
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: CancellationToken.None);
    }

    private async Task HandleApproveOrRejectAsync(string? args, Message message, User user, bool approve,
        CancellationToken ct)
    {
        var command = approve ? "/approve" : "/reject";

        if (!await bookingService.CanApproveAsync(user.Id, ct))
        {
            await SendTextAsync(message, "⛔ Подтверждать и отклонять заявки могут только организаторы.");
            return;
        }

        var pending = (await bookingRepository.GetPendingForCoachAsync(ct))
            .Where(b => b.ScheduledAt > DateTimeOffset.UtcNow)
            .OrderBy(b => b.ScheduledAt)
            .ToList();

        RehearsalBooking? booking;
        if (!string.IsNullOrWhiteSpace(args))
        {
            if (!BotDateParser.TryParseDateTime(args, BotDateParser.NowMsk(), out var at))
            {
                await SendUsageAsync(message, command);
                return;
            }

            var atUtc = ToUtcOffset(at);
            booking = pending.FirstOrDefault(b => b.ScheduledAt == atUtc);
            if (booking == null)
            {
                await SendTextAsync(message, $"🤷 Нет заявки на {at:dd.MM HH:mm}.");
                return;
            }
        }
        else if (pending.Count == 1)
        {
            booking = pending[0];
        }
        else
        {
            if (pending.Count == 0)
            {
                await SendTextAsync(message, "✨ Нет заявок, ждущих подтверждения.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Заявок несколько — укажите время: <code>{command} ДД.ММ ЧЧ:ММ</code>");
            sb.AppendLine();
            foreach (var b in pending)
                sb.AppendLine($"⏳ <code>{FormatMsk(b.ScheduledAt)}</code>");
            await SendTextAsync(message, sb.ToString());
            return;
        }

        var result = await DecideAsync(booking.Id, user.Id, approve, ct);
        await SendTextAsync(message, result);
    }

    /// <summary>
    /// Кнопки «Подтвердить/Отклонить» под заявкой /take_with.
    /// </summary>
    public async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        var parts = callback.Data![CallbackPrefix.Length..].Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var bookingId) || parts[0] is not ("approve" or "reject"))
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
            return;
        }

        var approve = parts[0] == "approve";
        if (!await bookingService.CanApproveAsync(callback.From.Id, ct))
        {
            await botClient.AnswerCallbackQuery(callback.Id,
                "Подтверждать заявки могут только организаторы.", showAlert: true, cancellationToken: ct);
            return;
        }

        var result = await DecideAsync(bookingId, callback.From.Id, approve, ct);
        await botClient.AnswerCallbackQuery(callback.Id, StripTags(result), cancellationToken: ct);

        if (callback.Message is { } original)
        {
            var verdict = approve ? "✅ Подтвердил(а)" : "❌ Отклонил(а)";
            await botClient.EditMessageText(
                original.Chat.Id,
                original.MessageId,
                $"{Html(original.Text)}\n\n{verdict} {Mention(callback.From)}",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    private async Task<string> DecideAsync(Guid bookingId, long tgUserId, bool approve, CancellationToken ct)
    {
        try
        {
            var booking = approve
                ? await bookingService.ApproveBookingAsync(bookingId, tgUserId, ct)
                : await bookingService.RejectBookingAsync(bookingId, tgUserId, ct);

            return approve
                ? $"✅ Заявка подтверждена: {FormatBookingRange(booking)}"
                : $"❌ Заявка отклонена: {FormatBookingRange(booking)}";
        }
        catch (BookingRuleException ex)
        {
            return $"⚠️ {Html(ex.Message)}";
        }
        catch (ForbiddenAccessException)
        {
            return "⛔ Подтверждать и отклонять заявки могут только организаторы.";
        }
    }

    private async Task HandleCancelAsync(string? args, Message message, User user, CancellationToken ct)
    {
        if (!BotDateParser.TryParseDateTime(args, BotDateParser.NowMsk(), out var scheduledAt))
        {
            await SendUsageAsync(message, "/cancel");
            return;
        }

        try
        {
            var booking = await bookingService.CancelBookingAsync(user.Id, scheduledAt, ct);
            await SendTextAsync(message, $"🗑 Бронь отменена: {FormatBookingRange(booking)}");
        }
        catch (BookingRuleException ex)
        {
            await SendTextAsync(message, $"❌ {Html(ex.Message)}");
        }
    }

    private async Task<Song?> ResolveSongAsync(Message message, string command, CancellationToken ct)
    {
        SongTopic? topic = null;
        if (message.IsTopicMessage && message.MessageThreadId is { } threadId)
            topic = await songTopicRepository.FindByTopicIdAsync(threadId, ct);

        if (topic == null)
        {
            await SendTextAsync(message, $"❌ Команду {command} нужно отправлять в топике песни.");
            return null;
        }

        var song = await songRepository.FindByIdAsync(topic.SongId, ct);
        if (song == null)
            await SendTextAsync(message, "❌ Песня для этого топика не найдена.");

        return song;
    }

    private Task SendUsageAsync(Message message, string command)
    {
        return SendTextAsync(message,
            $"📋 Формат: <code>{command} ДАТА ВРЕМЯ</code>\n" +
            $"Дата — {BotDateParser.DateHint}.\n" +
            $"Пример: <code>{command} 19.02 18:00</code>");
    }

    /// <summary>
    /// «/slots без @user1 @user2» → [user1, user2].
    /// </summary>
    private static List<string> ParseExcludedUsers(string? args)
    {
        if (string.IsNullOrWhiteSpace(args)) return [];

        var withoutIndex = args.IndexOf("без", StringComparison.OrdinalIgnoreCase);
        if (withoutIndex < 0) return [];

        return MentionRegex.Matches(args[(withoutIndex + "без".Length)..])
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateTimeOffset ToUtcOffset(DateTime msk)
    {
        return new DateTimeOffset(msk.FromMskToUtc(), TimeSpan.Zero);
    }

    private static DateTimeOffset RoundUpToQuarter(DateTimeOffset value)
    {
        var quarter = TimeSpan.FromMinutes(15).Ticks;
        return new DateTimeOffset((value.UtcTicks + quarter - 1) / quarter * quarter, TimeSpan.Zero);
    }

    private static string FormatMsk(DateTimeOffset value)
    {
        return value.UtcDateTime.FromUtcToMsk().ToString("dd.MM HH:mm");
    }

    private static string Range(DateTimeOffset start, DateTimeOffset end)
    {
        return $"{start.UtcDateTime.FromUtcToMsk():HH:mm}–{end.UtcDateTime.FromUtcToMsk():HH:mm}";
    }

    private static string FormatBookingRange(RehearsalBooking booking)
    {
        var start = booking.ScheduledAt.UtcDateTime.FromUtcToMsk();
        return $"{BotDateParser.FormatDate(start)}, " +
               Range(booking.ScheduledAt, booking.ScheduledAt.AddMinutes(booking.DurationMinutes));
    }

    private static string Mention(User user)
    {
        var name = string.IsNullOrWhiteSpace(user.FirstName) ? user.Username ?? "участник" : user.FirstName;
        return $"<a href=\"tg://user?id={user.Id}\">{Html(name)}</a>";
    }

    private static string StripTags(string html)
    {
        return WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", ""));
    }

    private static string Html(string? text)
    {
        return WebUtility.HtmlEncode(text ?? "");
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
