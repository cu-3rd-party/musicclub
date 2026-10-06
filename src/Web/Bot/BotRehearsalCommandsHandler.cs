using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Обработчик команд бронирования репетиций:
/// /check, /slots, /slots_with, /take, /take_with, /approve, /reject, /cancel
/// </summary>
public class BotRehearsalCommandsHandler(
    IRehearsalBookingService bookingService,
    ISongRepository songRepository,
    ISongTopicRepository songTopicRepository,
    ITelegramChatService telegramChatService,
    ITelegramBotClient botClient,
    IRehearsalBookingRepository bookingRepository,
    ILogger<BotRehearsalCommandsHandler> logger)
{
    private static readonly Regex CommandRegex = new(
        @"^\/(?<command>[a-z0-9_]+)(?:@(?<botusername>[a-zA-Z0-9_]+))?(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DateRegex = new(@"^(\d{1,2})[./-](\d{1,2})$", RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"^(\d{1,2}):?(\d{2})$", RegexOptions.Compiled);
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
                    await HandleApproveAsync(args, message, user, ct);
                    break;
                case "reject":
                    await HandleRejectAsync(args, message, user, ct);
                    break;
                case "cancel":
                    await HandleCancelAsync(args, message, user, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling rehearsal command {Command}", command);
            await botClient.SendMessage(
                message.Chat.Id,
                "❌ Произошла ошибка. Попробуйте позже.",
                cancellationToken: ct);
        }
    }

    private async Task HandleCheckAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var topic = ResolveTopic(message);
        if (topic == null)
        {
            await SendTextAsync(message, "❌ Команда /check должна использоваться в теме песни.");
            return;
        }

        var song = await songRepository.FindByIdAsync(topic.SongId, ct);
        if (song == null)
        {
            await SendTextAsync(message, "❌ Песня не найдена.");
            return;
        }

        var parsed = ParseDateAndTime(args);
        if (parsed == null)
        {
            await SendTextAsync(message,
                "📋 Формат: /check ДД.MM ЧЧ:ММ или /check пн 17-19\n" +
                "Пример: /check 19.02 18:00");
            return;
        }

        var (date, hour, minute) = parsed.Value;
        var result = await bookingService.CheckAvailabilityAsync(song.Id, date, hour, minute, ct);

        var statusIcon = result.IsAvailable ? "✅" : "⛔";
        var statusText = result.IsAvailable
            ? "Свободен"
            : $"Занят: {string.Join(", ", result.BusyUsers)}";

        await SendTextAsync(message, $"{statusIcon} {result.Message}");
    }

    private async Task HandleSlotsAsync(string? args, Message message, User user, CancellationToken ct, bool withCoach)
    {
        var topic = ResolveTopic(message);
        if (topic == null)
        {
            await SendTextAsync(message, "❌ Команда /slots должна использоваться в теме песни.");
            return;
        }

        var song = await songRepository.FindByIdAsync(topic.SongId, ct);
        if (song == null)
        {
            await SendTextAsync(message, "❌ Песня не найдена.");
            return;
        }

        var from = DateTime.Today;
        var excludeUsernames = ParseExcludedUsers(args);

        var result = withCoach
            ? await bookingService.FindWeeklySlotsWithCoachAsync(song.Id, from, excludeUsernames, ct)
            : await bookingService.FindWeeklySlotsAsync(song.Id, from, excludeUsernames, ct);

        var title = withCoach
            ? "📅 Свободные слоты на неделю (с Ильёй)"
            : "📅 Свободные слоты на неделю";

        if (result.AvailableSlots.Count == 0)
        {
            await SendTextAsync(message, $"{title}\n\n{result.Message}");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{title}");
        sb.AppendLine();

        foreach (var slot in result.AvailableSlots.Take(20))
        {
            sb.AppendLine($"  ⏰ {slot:dd.MM ddd HH:mm}");
        }

        if (result.AvailableSlots.Count > 20)
            sb.AppendLine($"  ... и ещё {result.AvailableSlots.Count - 20}");

        await SendTextAsync(message, sb.ToString());
    }

    private async Task HandleTakeAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var topic = ResolveTopic(message);
        if (topic == null)
        {
            await SendTextAsync(message, "❌ Команда /take должна использоваться в теме песни.");
            return;
        }

        var song = await songRepository.FindByIdAsync(topic.SongId, ct);
        if (song == null)
        {
            await SendTextAsync(message, "❌ Песня не найдена.");
            return;
        }

        var parsed = ParseDateAndTime(args);
        if (parsed == null)
        {
            await SendTextAsync(message,
                "📋 Формат: /take ДД.MM ЧЧ:ММ\n" +
                "Пример: /take 19.02 18:00");
            return;
        }

        var (date, hour, minute) = parsed.Value;
        var scheduledAt = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

        try
        {
            var booking = await bookingService.CreateBookingAsync(
                user.Id, scheduledAt, song.Id, ct: ct);

            await SendTextAsync(message,
                $"✅ Репетиция забронирована!\n" +
                $"📅 {booking.ScheduledAt:dd.MM HH:mm}\n" +
                $"🎵 {song.Title} — {song.Artist}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create booking");
            await SendTextAsync(message, $"❌ Не удалось забронировать: {ex.Message}");
        }
    }

    private async Task HandleTakeWithAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var topic = ResolveTopic(message);
        if (topic == null)
        {
            await SendTextAsync(message, "❌ Команда /take_with должна использоваться в теме песни.");
            return;
        }

        var song = await songRepository.FindByIdAsync(topic.SongId, ct);
        if (song == null)
        {
            await SendTextAsync(message, "❌ Песня не найдена.");
            return;
        }

        var parsed = ParseDateAndTime(args);
        if (parsed == null)
        {
            await SendTextAsync(message,
                "📋 Формат: /take_with ДД.MM ЧЧ:ММ\n" +
                "Пример: /take_with 19.02 18:00");
            return;
        }

        var (date, hour, minute) = parsed.Value;
        var scheduledAt = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

        try
        {
            var booking = await bookingService.CreateBookingWithCoachAsync(
                user.Id, scheduledAt, song.Id, ct: ct);

            // TODO: отправить уведомление Илье с кнопками approve/reject
            await SendTextAsync(message,
                $"📝 Запрос на репетицию отправлен!\n" +
                $"📅 {booking.ScheduledAt:dd.MM HH:mm}\n" +
                $"⏳ Ожидает подтверждения Ильёй.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create booking with coach");
            await SendTextAsync(message, $"❌ Не удалось создать запрос: {ex.Message}");
        }
    }

    private async Task HandleApproveAsync(string? args, Message message, User user, CancellationToken ct)
    {
        // TODO: проверить, что пользователь — Илья
        var booking = await ResolvePendingBooking(message, user.Id, ct);
        if (booking == null) return;

        try
        {
            var approved = await bookingService.ApproveBookingAsync(booking.Id, user.Id, ct);
            await SendTextAsync(message,
                $"✅ Бронирование подтверждено!\n" +
                $"📅 {approved.ScheduledAt:dd.MM HH:mm}\n" +
                $"🎸 Репетиция добавлена в календарь.");
        }
        catch (Exception ex)
        {
            await SendTextAsync(message, $"❌ Ошибка: {ex.Message}");
        }
    }

    private async Task HandleRejectAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var booking = await ResolvePendingBooking(message, user.Id, ct);
        if (booking == null) return;

        try
        {
            var rejected = await bookingService.RejectBookingAsync(booking.Id, user.Id, ct);
            await SendTextAsync(message,
                $"❌ Бронирование отклонено.\n" +
                $"📅 {rejected.ScheduledAt:dd.MM HH:mm}");
        }
        catch (Exception ex)
        {
            await SendTextAsync(message, $"❌ Ошибка: {ex.Message}");
        }
    }

    private async Task HandleCancelAsync(string? args, Message message, User user, CancellationToken ct)
    {
        var parsed = ParseDateAndTime(args);
        if (parsed == null)
        {
            await SendTextAsync(message,
                "📋 Формат: /cancel ДД.MM ЧЧ:ММ\n" +
                "Пример: /cancel 19.02 18:00");
            return;
        }

        var (date, hour, minute) = parsed.Value;
        var scheduledAt = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);

        try
        {
            await bookingService.CancelBookingAsync(user.Id, scheduledAt, ct);
            await SendTextAsync(message, $"🗑️ Бронирование отменено: {scheduledAt:dd.MM HH:mm}");
        }
        catch (Exception ex)
        {
            await SendTextAsync(message, $"❌ {ex.Message}");
        }
    }

    private SongTopic? ResolveTopic(Message message)
    {
        if (!message.IsTopicMessage || message.MessageThreadId == null) return null;

        return songTopicRepository.FindByTopicIdAsync(
                (long) message.MessageThreadId, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private async Task<RehearsalBooking?> ResolvePendingBooking(
        Message message, long userId, CancellationToken ct)
    {
        // Ищем первое pending-бронирование пользователя
        // TODO: в будущем — поиск по ID из аргумента команды
        var pending = await bookingRepository.GetPendingByRequesterAsync(userId, ct);
        return pending.FirstOrDefault();
    }

    private DateTime? ParseDatePart(string dateStr)
    {
        // ДД.MM
        if (DateRegex.Match(dateStr) is { Success: true } dateMatch)
        {
            var day = int.Parse(dateMatch.Groups[1].Value);
            var month = int.Parse(dateMatch.Groups[2].Value);
            var year = DateTime.Today.Year;
            return new DateTime(year, month, day);
        }

        // пн, вт, ... (ближайший день)
        if (DayAliases.TryGetValue(dateStr, out var dayOfWeek))
        {
            var today = (int) DateTime.Today.DayOfWeek;
            today = today == 0 ? 7 : today; // Sunday = 7
            var diff = dayOfWeek - today;
            if (diff <= 0) diff += 7;
            return DateTime.Today.AddDays(diff);
        }

        return null;
    }

    private (DateTime date, int hour, int minute)? ParseDateAndTime(string? args)
    {
        if (string.IsNullOrWhiteSpace(args)) return null;

        var parts = args.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) return null;

        var date = ParseDatePart(parts[0]);
        if (date == null) return null;

        var hour = 18;
        var minute = 0;

        if (parts.Length >= 2)
        {
            var timeStr = parts[1];
            if (TimeRegex.Match(timeStr) is { Success: true } timeMatch)
            {
                hour = int.Parse(timeMatch.Groups[1].Value);
                minute = int.Parse(timeMatch.Groups[2].Value);
            }
            else if (int.TryParse(timeStr, out var h))
            {
                hour = h;
            }
        }

        if (parts.Length >= 3 && parts[1].Contains("-"))
        {
            var rangeParts = parts[1].Split('-');
            if (rangeParts.Length == 2 && int.TryParse(rangeParts[0], out var startH))
                hour = startH;
        }

        return (date.Value, hour, minute);
    }

    private List<string>? ParseExcludedUsers(string? args)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(args)) return null;

        var parts = args.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var skipMode = false;

        foreach (var part in parts)
        {
            if (part.Equals("без", StringComparison.OrdinalIgnoreCase))
            {
                skipMode = true;
                continue;
            }

            if (skipMode && part.StartsWith("@"))
            {
                result.Add(part[1..]);
                continue;
            }

            if (skipMode)
            {
                result.Add(part);
                continue;
            }

            skipMode = false;
        }

        return result.Count > 0 ? result : null;
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
