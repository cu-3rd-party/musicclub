using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
    ILogger<BotScheduleCommandsHandler> logger)
{
    private static readonly Regex DateRegex = new(@"^(\d{1,2})[./-](\d{1,2})$", RegexOptions.Compiled);
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
            await SendTextAsync(message, $"📅 Расписание на {date:dd.MM ddd}:\n\nНичего нет. 🎸");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📅 Расписание на {date:dd.MM ddd, ddd}");
        sb.AppendLine();

        foreach (var booking in bookings.OrderBy(b => b.ScheduledAt))
        {
            var time = booking.ScheduledAt.ToString("HH:mm");
            var end = booking.ScheduledAt.AddMinutes(booking.DurationMinutes).ToString("HH:mm");
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
        var date = ParseDate(args);

        var bookings = await bookingRepository.GetUpcomingAsync(
            date.Date,
            date.Date.AddDays(1),
            ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Расписание на {date:dd.MM ddd, ddd}");
        sb.AppendLine();

        if (bookings.Count == 0)
        {
            sb.AppendLine("Ничего не запланировано.");
        }
        else
        {
            foreach (var booking in bookings.OrderBy(b => b.ScheduledAt))
            {
                var time = booking.ScheduledAt.ToString("HH:mm");
                var end = booking.ScheduledAt.AddMinutes(booking.DurationMinutes).ToString("HH:mm");
                sb.AppendLine($"⏰ {time}–{end}");
            }
        }

        // TODO: сгенерировать PNG-картинку с визуальным расписанием
        await SendTextAsync(message, sb.ToString());
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
        if (string.IsNullOrWhiteSpace(args))
            return DateTime.Today;

        if (DateRegex.Match(args) is { Success: true } dateMatch)
        {
            var day = int.Parse(dateMatch.Groups[1].Value);
            var month = int.Parse(dateMatch.Groups[2].Value);
            return new DateTime(DateTime.Today.Year, month, day);
        }

        if (DayAliases.TryGetValue(args.Trim(), out var dow))
        {
            var today = (int) DateTime.Today.DayOfWeek;
            today = today == 0 ? 7 : today;
            var diff = dow - today;
            if (diff <= 0) diff += 7;
            return DateTime.Today.AddDays(diff);
        }

        return DateTime.Today;
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
