using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Application.Services.Calendar;

public interface IRehearsalReportService
{
    /// <summary>
    /// Формирует отчёты за все наступившие периоды и досылает их тем, кому они ещё не отправлены.
    /// Проблема с одним получателем не мешает остальным: он получит отчёт при следующем запуске.
    /// </summary>
    Task ProcessDueReportsAsync(DateTimeOffset now, CancellationToken ct = default);
}

public class RehearsalReportService(
    IRehearsalReportRepository reportRepository,
    IRoomScheduleProvider roomSchedule,
    ISongRepository songRepository,
    ISongRoadieRepository songRoadieRepository,
    IRehearsalBookingRepository bookingRepository,
    IApplicationUserRepository userRepository,
    ITelegramChatService telegramChatService,
    IOptions<RehearsalReportOptions> options,
    ILogger<RehearsalReportService> logger) : IRehearsalReportService
{
    private static readonly TimeSpan Moscow = TimeSpan.FromHours(3);
    private static readonly TimeOnly SendTime = new(10, 0);

    private readonly RehearsalReportOptions _options = options.Value;

    /// <summary>Ближайший после <paramref name="now"/> момент рассылки: 1-е число, 10:00 МСК.</summary>
    public static DateTimeOffset NextSendTime(DateTimeOffset now)
    {
        var moscowNow = now.ToOffset(Moscow);
        var thisMonth = new DateTimeOffset(moscowNow.Year, moscowNow.Month, 1, SendTime.Hour, SendTime.Minute, 0, Moscow);
        return thisMonth > now ? thisMonth : thisMonth.AddMonths(1);
    }

    /// <summary>Периоды (первые дни месяцев), отчёт за которые уже должен быть отправлен.</summary>
    public static IEnumerable<DateOnly> GetDuePeriods(DateOnly firstPeriod, DateTimeOffset now)
    {
        var first = new DateOnly(firstPeriod.Year, firstPeriod.Month, 1);
        var nextSend = NextSendTime(now);
        var lastDue = new DateOnly(nextSend.Year, nextSend.Month, 1).AddMonths(-2);

        for (var period = first; period <= lastDue; period = period.AddMonths(1))
            yield return period;
    }

    public async Task ProcessDueReportsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var recipients = _options.Recipients
            .Select(r => r.Trim().TrimStart('@').ToLowerInvariant())
            .Where(r => r.Length > 0)
            .Distinct()
            .ToList();
        var delivered = await reportRepository.GetDeliveredRecipientsAsync(ct);

        foreach (var period in GetDuePeriods(_options.FirstPeriod, now))
        {
            var pending = delivered.TryGetValue(period, out var done)
                ? recipients.Where(r => !done.Contains(r)).ToList()
                : recipients;
            if (pending.Count == 0)
                continue;

            var report = await reportRepository.FindByPeriodStartAsync(period, ct)
                         ?? await CreateReportAsync(period, now, ct);

            foreach (var recipient in pending)
                await DeliverAsync(report, recipient, ct);
        }
    }

    private async Task<RehearsalReport> CreateReportAsync(DateOnly periodStart, DateTimeOffset now, CancellationToken ct)
    {
        // Без календаря отчёт был бы пустым, а сохранённый отчёт уже не пересобирается.
        if (!roomSchedule.IsConfigured)
            throw new InvalidOperationException("Не задан календарь зала (YandexCalendar:RoomIcsToken)");

        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var from = new DateTimeOffset(periodStart, TimeOnly.MinValue, Moscow).ToUniversalTime();
        var to = new DateTimeOffset(periodEnd.AddDays(1), TimeOnly.MinValue, Moscow).ToUniversalTime();

        var roomEvents = await roomSchedule.GetRoomEventsAsync(from, to, ct);
        var unmatched = new List<string>();
        var rows = RehearsalReportBuilder.BuildRows(
            roomEvents,
            await LoadSongsAsync(ct),
            await LoadRequesterLoginsAsync(from, to, ct),
            unmatched);

        foreach (var title in unmatched.Distinct())
            logger.LogWarning("Отчёт {Period}: не нашли песню для события «{Title}»", periodStart, title);

        var report = new RehearsalReport
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            FileName = RehearsalReportBuilder.BuildFileName(periodStart, periodEnd),
            Content = RehearsalReportBuilder.BuildWorkbook(rows, periodStart, periodEnd),
            CreatedAt = now.ToUniversalTime()
        };

        await reportRepository.AddAsync(report, ct);
        await reportRepository.SaveChangesAsync(ct);

        logger.LogInformation("Сформирован отчёт о репетициях за {From}–{To}: {Rows} строк",
            periodStart, periodEnd, rows.Count);
        return report;
    }

    private async Task DeliverAsync(RehearsalReport report, string recipient, CancellationToken ct)
    {
        var user = await userRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName != null && u.UserName.ToLower() == recipient && u.TgUserId != null, ct);
        if (user?.TgUserId is not { } tgUserId)
        {
            logger.LogWarning(
                "Отчёт {File}: @{Recipient} не найден среди пользователей — ему нужно открыть бота (/start) и приложение",
                report.FileName, recipient);
            return;
        }

        var caption = $"Отчёт о репетициях MusicClub за {report.PeriodStart:dd.MM.yyyy}–{report.PeriodEnd:dd.MM.yyyy}";
        try
        {
            await telegramChatService.SendDirectDocument(tgUserId, report.Content, report.FileName, caption, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Отчёт {File}: не удалось отправить @{Recipient}", report.FileName, recipient);
            return;
        }

        await reportRepository.AddDeliveryAsync(new RehearsalReportDelivery
        {
            ReportId = report.Id,
            Recipient = recipient,
            TgUserId = tgUserId,
            SentAt = DateTimeOffset.UtcNow
        }, ct);
        await reportRepository.SaveChangesAsync(ct);

        logger.LogInformation("Отчёт {File} отправлен @{Recipient}", report.FileName, recipient);
    }

    private async Task<List<RehearsalReportSong>> LoadSongsAsync(CancellationToken ct)
    {
        var songs = await songRepository.Query()
            .AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.Artist,
                Members = s.Assignments
                    .Where(a => a.User.YandexLogin != null && a.User.YandexLogin != "")
                    .Select(a => a.User.YandexLogin!)
                    .ToList()
            })
            .ToListAsync(ct);

        var roadies = await songRoadieRepository.Query()
            .AsNoTracking()
            .Where(r => r.Roadie.YandexLogin != null && r.Roadie.YandexLogin != "")
            .Select(r => new { r.SongId, Login = r.Roadie.YandexLogin! })
            .ToDictionaryAsync(r => r.SongId, r => r.Login, ct);

        return songs
            .Select(s => new RehearsalReportSong(s.Title, s.Artist, s.Members, roadies.GetValueOrDefault(s.Id)))
            .ToList();
    }

    private async Task<Dictionary<DateTimeOffset, string>> LoadRequesterLoginsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var result = new Dictionary<DateTimeOffset, string>();
        foreach (var booking in await bookingRepository.GetActiveInRangeAsync(from, to, ct))
        {
            var requester = await userRepository.FindByTgUserIdAsync(booking.RequesterTgUserId, ct);
            if (!string.IsNullOrEmpty(requester?.YandexLogin))
                result[booking.ScheduledAt] = requester.YandexLogin;
        }

        return result;
    }
}
