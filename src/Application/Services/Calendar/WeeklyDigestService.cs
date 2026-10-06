using System.Text;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сервис генерации и публикации еженедельной сводки репетиций.
/// </summary>
public interface IWeeklyDigestService
{
    /// <summary>
    /// Генерирует текстовое расписание на неделю.
    /// </summary>
    string GenerateDigestText(IReadOnlyList<RehearsalBooking> bookings, DateTime from, DateTime to);

    /// <summary>
    /// Публикует сводку в Telegram-чат.
    /// </summary>
    Task<PublishDigestResult> PublishDigestAsync(
        long chatId,
        int? topicId,
        DateTime weekFrom,
        CancellationToken ct = default);

    /// <summary>
    /// Обновляет существующую сводку (редактирует сообщение).
    /// </summary>
    Task<UpdateDigestResult> UpdateDigestAsync(
        long chatId,
        int messageId,
        DateTime weekFrom,
        CancellationToken ct = default);
}

public record PublishDigestResult(
    bool Success,
    int? MessageId,
    string Message);

public record UpdateDigestResult(
    bool Success,
    string Message);

public class WeeklyDigestService(
    IRehearsalBookingRepository bookingRepository,
    ISongRepository songRepository,
    ITelegramChatService telegramChatService,
    ILogger<WeeklyDigestService> logger) : IWeeklyDigestService
{
    private const int MaxMessageLength = 4096;

    public string GenerateDigestText(IReadOnlyList<RehearsalBooking> bookings, DateTime from, DateTime to)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📅 <b>Расписание репетиций</b>");
        sb.AppendLine($"<i>{from:dd.MM} — {to:dd.MM yyyy}</i>");
        sb.AppendLine();

        if (bookings.Count == 0)
        {
            sb.AppendLine("На эту неделю репетиций нет. 🎸");
            return sb.ToString();
        }

        // Группируем по дням
        var grouped = bookings
            .OrderBy(b => b.ScheduledAt)
            .GroupBy(b => b.ScheduledAt.ToOffset(TimeSpan.FromHours(3)).Date);

        foreach (var dayGroup in grouped)
        {
            sb.AppendLine($"<b>{dayGroup.Key:dd.MM yyyy, ddd}</b>");

            foreach (var booking in dayGroup)
            {
                var time = booking.ScheduledAt.ToOffset(TimeSpan.FromHours(3)).ToString("HH:mm");
                var duration = booking.DurationMinutes;
                var end = booking.ScheduledAt.ToOffset(TimeSpan.FromHours(3)).AddMinutes(duration);

                sb.Append($"  ⏰ <code>{time}–{end:HH:mm}</code>");

                if (booking.SongId.HasValue)
                {
                    // Название песни загружается лениво
                    sb.Append(" 🎵");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task<PublishDigestResult> PublishDigestAsync(
        long chatId,
        int? topicId,
        DateTime weekFrom,
        CancellationToken ct = default)
    {
        var to = weekFrom.Date.AddDays(7);

        var bookings = await bookingRepository.GetUpcomingAsync(weekFrom.Date, to, ct);

        var text = GenerateDigestText(bookings, weekFrom.Date, to);

        try
        {
            if (topicId.HasValue)
                await telegramChatService.SendTopicMessage((long) topicId, text, ct);
            else
                await telegramChatService.SendGeneralMessage(text, ct);

            return new PublishDigestResult(true, null, "Сводка опубликована.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось опубликовать сводку");
            return new PublishDigestResult(false, null, "Ошибка публикации сводки.");
        }
    }

    public async Task<UpdateDigestResult> UpdateDigestAsync(
        long chatId,
        int messageId,
        DateTime weekFrom,
        CancellationToken ct = default)
    {
        var to = weekFrom.Date.AddDays(7);

        var bookings = await bookingRepository.GetUpcomingAsync(weekFrom.Date, to, ct);

        var text = GenerateDigestText(bookings, weekFrom.Date, to);

        try
        {
            // Telegram Bot API: редактирование сообщения через sendMessage с edit_message_id
            // В текущей реализации TelegramChatService нет метода edit, используем SendMessage
            // TODO: добавить EditMessageAsync в ITelegramChatService
            logger.LogWarning("Edit message не реализован в TelegramChatService");
            return new UpdateDigestResult(false, "Редактирование сообщений пока не поддерживается.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось обновить сводку");
            return new UpdateDigestResult(false, "Ошибка обновления сводки.");
        }
    }
}
