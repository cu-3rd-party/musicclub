using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сервис управления бронированиями репетиций.
/// Ядро логики: создание, проверка доступности, поиск слотов, approval-флоу.
/// </summary>
public interface IRehearsalBookingService
{
    /// <summary>
    /// Проверяет доступность всех участников песни на указанную дату/время.
    /// </summary>
    Task<AvailabilityCheckResult> CheckAvailabilityAsync(
        Guid songId,
        DateTime date,
        int hour,
        int minute,
        CancellationToken ct = default);

    /// <summary>
    /// Находит все доступные слоты на неделю для участников песни.
    /// </summary>
    Task<WeeklySlotsResult> FindWeeklySlotsAsync(
        Guid songId,
        DateTime from,
        IEnumerable<string>? excludeUsernames = null,
        CancellationToken ct = default);

    /// <summary>
    /// Находит слоты с проверкой доступности Ильи (тренера).
    /// </summary>
    Task<WeeklySlotsResult> FindWeeklySlotsWithCoachAsync(
        Guid songId,
        DateTime from,
        IEnumerable<string>? excludeUsernames = null,
        CancellationToken ct = default);

    /// <summary>
    /// Создаёт бронирование репетиции (confirmed, без запроса к Илье).
    /// </summary>
    Task<RehearsalBooking> CreateBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = 80,
        CancellationToken ct = default);

    /// <summary>
    /// Создаёт бронирование с запросом к Илье (pending).
    /// </summary>
    Task<RehearsalBooking> CreateBookingWithCoachAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = 80,
        CancellationToken ct = default);

    /// <summary>
    /// Подтверждает pending-бронирование (Илья).
    /// </summary>
    Task<RehearsalBooking> ApproveBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Отклоняет pending-бронирование (Илья).
    /// </summary>
    Task<RehearsalBooking> RejectBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Отменяет бронирование инициатором.
    /// </summary>
    Task CancelBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        CancellationToken ct = default);

    /// <summary>
    /// Получает список участников песни, у которых есть Yandex login.
    /// </summary>
    Task<IReadOnlyList<UserWithYandex>> GetMembersWithYandexAsync(
        Guid songId,
        CancellationToken ct = default);

    /// <summary>
    /// Получает upcoming-бронирования на указанную неделю.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetUpcomingBookingsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

/// <summary>
/// Результат проверки доступности.
/// </summary>
public record AvailabilityCheckResult(
    bool IsAvailable,
    IReadOnlyList<string> BusyUsers,
    string Message);

/// <summary>
/// Результат поиска слотов.
/// </summary>
public record WeeklySlotsResult(
    IReadOnlyList<DateTime> AvailableSlots,
    IReadOnlyList<string> BusyUsers,
    string Message);

/// <summary>
/// Пользователь с Yandex login.
/// </summary>
public record UserWithYandex(
    Guid UserId,
    string DisplayName,
    string YandexLogin,
    string Email);

public class RehearsalBookingService(
    IRehearsalBookingRepository bookingRepository,
    ICalendarSyncService calendarSyncService,
    ISongRoleAssignmentRepository songRoleAssignmentRepository,
    IApplicationUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<RehearsalBookingService> logger) : IRehearsalBookingService
{
    private const int DefaultDurationMinutes = 80;
    private const int SlotSearchStartHour = 10;
    private const int SlotSearchEndHour = 22;
    private const int MinSlotMinutes = 60;

    public async Task<AvailabilityCheckResult> CheckAvailabilityAsync(
        Guid songId,
        DateTime date,
        int hour,
        int minute,
        CancellationToken ct = default)
    {
        var start = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);
        var end = start.AddMinutes(DefaultDurationMinutes);

        var memberUserIds = await songRoleAssignmentRepository
            .GetMemberUserIdsBySongIdAsync(songId, ct);

        var busyUsers = new List<string>();

        foreach (var userId in memberUserIds)
        {
            var user = await userRepository.FindByIdAsync(userId, ct);
            if (user == null) continue;

            // Проверяем только тех, у кого есть Yandex login
            if (string.IsNullOrEmpty(user.YandexLogin))
                continue;

            var isBusy = await calendarSyncService.IsUserBusyAsync(userId, start, end, ct);
            if (isBusy)
                busyUsers.Add(user.DisplayName);
        }

        var isAvailable = busyUsers.Count == 0;

        return new AvailabilityCheckResult(
            isAvailable,
            busyUsers,
            isAvailable
                ? $"Слот {start:dd.MM HH:mm} свободен для всех участников."
                : $"Слот {start:dd.MM HH:mm} занят: {string.Join(", ", busyUsers)}");
    }

    public async Task<WeeklySlotsResult> FindWeeklySlotsAsync(
        Guid songId,
        DateTime from,
        IEnumerable<string>? excludeUsernames = null,
        CancellationToken ct = default)
    {
        return await FindSlotsInternalAsync(songId, from, withCoach: false, excludeUsernames, ct);
    }

    public async Task<WeeklySlotsResult> FindWeeklySlotsWithCoachAsync(
        Guid songId,
        DateTime from,
        IEnumerable<string>? excludeUsernames = null,
        CancellationToken ct = default)
    {
        return await FindSlotsInternalAsync(songId, from, withCoach: true, excludeUsernames, ct);
    }

    public async Task<RehearsalBooking> CreateBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = DefaultDurationMinutes,
        CancellationToken ct = default)
    {
        var booking = new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            ScheduledAt = new DateTimeOffset(scheduledAt, TimeSpan.FromHours(3)), // MSK
            DurationMinutes = durationMinutes,
            Status = BookingStatus.Confirmed,
            RequesterTgUserId = requesterTgUserId,
            SongId = songId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        await bookingRepository.AddAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Создаём CalDAV-событие
        try
        {
            await calendarSyncService.CreateBookingEventAsync(booking, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось создать CalDAV-событие для бронирования {BookingId}", booking.Id);
        }

        await transaction.CommitAsync(ct);
        return booking;
    }

    public async Task<RehearsalBooking> CreateBookingWithCoachAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = DefaultDurationMinutes,
        CancellationToken ct = default)
    {
        // TODO: найти ID Ильи (тренера) — можно вынести в опции
        // Пока заглушка: Илья — первый администратор
        var coachUserId = await FindCoachUserIdAsync(ct);

        var booking = new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            ScheduledAt = new DateTimeOffset(scheduledAt, TimeSpan.FromHours(3)),
            DurationMinutes = durationMinutes,
            Status = BookingStatus.Pending,
            RequesterTgUserId = requesterTgUserId,
            CoachUserId = coachUserId,
            SongId = songId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        await bookingRepository.AddAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return booking;
    }

    public async Task<RehearsalBooking> ApproveBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.FindByIdAsync(bookingId, ct)
            ?? throw new ArgumentException($"Бронирование {bookingId} не найдено", nameof(bookingId));

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Бронирование в статусе {booking.Status}, нельзя подтвердить.");

        // TODO: проверить, что approver — Илья

        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(ct);

        // Создаём CalDAV-событие
        try
        {
            await calendarSyncService.CreateBookingEventAsync(booking, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось создать CalDAV-событие после подтверждения бронирования {BookingId}", bookingId);
        }

        return booking;
    }

    public async Task<RehearsalBooking> RejectBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default)
    {
        var booking = await bookingRepository.FindByIdAsync(bookingId, ct)
            ?? throw new ArgumentException($"Бронирование {bookingId} не найдено", nameof(bookingId));

        if (booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Бронирование в статусе {booking.Status}, нельзя отклонить.");

        booking.Status = BookingStatus.Rejected;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(ct);

        return booking;
    }

    public async Task CancelBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        CancellationToken ct = default)
    {
        var bookings = await bookingRepository.GetByRequesterAsync(
            requesterTgUserId,
            scheduledAt.Date,
            scheduledAt.Date.AddDays(1),
            ct);

        var matching = bookings.FirstOrDefault(b =>
            b.ScheduledAt.Hour == scheduledAt.Hour
            && b.ScheduledAt.Minute == scheduledAt.Minute
            && b.Status == BookingStatus.Confirmed);

        if (matching == null)
            throw new InvalidOperationException("Бронирование не найдено.");

        matching.Status = BookingStatus.Cancelled;
        matching.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(matching);

        // Удаляем CalDAV-событие
        try
        {
            await calendarSyncService.DeleteBookingEventAsync(matching, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось удалить CalDAV-событие при отмене бронирования {BookingId}", matching.Id);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserWithYandex>> GetMembersWithYandexAsync(
        Guid songId,
        CancellationToken ct = default)
    {
        var memberUserIds = await songRoleAssignmentRepository
            .GetMemberUserIdsBySongIdAsync(songId, ct);

        var usersWithYandex = new List<UserWithYandex>();

        foreach (var userId in memberUserIds)
        {
            var user = await userRepository.FindByIdAsync(userId, ct);
            if (user == null || string.IsNullOrEmpty(user.YandexLogin))
                continue;

            usersWithYandex.Add(new UserWithYandex(
                user.Id,
                user.DisplayName,
                user.YandexLogin,
                BuildYandexEmail(user.YandexLogin)));
        }

        return usersWithYandex;
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetUpcomingBookingsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        return await bookingRepository.GetUpcomingAsync(from, to, ct);
    }

    private async Task<WeeklySlotsResult> FindSlotsInternalAsync(
        Guid songId,
        DateTime from,
        bool withCoach,
        IEnumerable<string>? excludeUsernames,
        CancellationToken ct)
    {
        var memberUserIds = await songRoleAssignmentRepository
            .GetMemberUserIdsBySongIdAsync(songId, ct);

        var busyUsers = new List<string>();
        var excludedUserIds = new HashSet<Guid>();

        // Определяем исключённых пользователей
        if (excludeUsernames != null)
        {
            foreach (var username in excludeUsernames)
            {
                var user = userRepository.Query()
                    .FirstOrDefault(u => u.UserName == username || u.DisplayName == username);
                if (user != null)
                    excludedUserIds.Add(user.Id);
            }
        }

        var availableMemberIds = memberUserIds.Except(excludedUserIds).ToList();

        if (availableMemberIds.Count == 0)
            return new WeeklySlotsResult([], ["Нет участников"], "Нет участников с назначенными ролями.");

        // Собираем все занятые интервалы
        var busyIntervals = new List<(DateTime Start, DateTime End)>();

        foreach (var userId in availableMemberIds)
        {
            var user = await userRepository.FindByIdAsync(userId, ct);
            if (user == null || string.IsNullOrEmpty(user.YandexLogin))
                continue;

            var isBusy = await calendarSyncService.IsUserBusyAsync(userId, from, from.AddDays(7), ct);
            if (isBusy)
                busyUsers.Add(user.DisplayName);
        }

        // Если coach required — проверяем Илью
        if (withCoach)
        {
            var coachUserId = await FindCoachUserIdAsync(ct);
            var isBusy = await calendarSyncService.IsUserBusyAsync(coachUserId, from, from.AddDays(7), ct);
            if (isBusy)
                busyUsers.Add("Илья (тренер)");
        }

        // Генерируем候选ные слоты (ежедневно 10:00-22:00, шаг 60 мин)
        var availableSlots = new List<DateTime>();
        var currentDay = from.Date;
        var endDay = from.Date.AddDays(7);

        while (currentDay < endDay)
        {
            for (var hour = SlotSearchStartHour; hour <= SlotSearchEndHour - 1; hour++)
            {
                var slotStart = new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, hour, 0, 0);
                var slotEnd = slotStart.AddMinutes(MinSlotMinutes);

                // Проверяем пересечение с занятыми интервалами
                var isFree = !busyIntervals.Any(i =>
                    slotStart < i.End && slotEnd > i.Start);

                if (isFree)
                    availableSlots.Add(slotStart);
            }

            currentDay = currentDay.AddDays(1);
        }

        return new WeeklySlotsResult(
            availableSlots,
            busyUsers,
            availableSlots.Count > 0
                ? $"Найдено {availableSlots.Count} свободных слотов."
                : "Свободных слотов не найдено.");
    }

    private async Task<Guid> FindCoachUserIdAsync(CancellationToken ct)
    {
        // TODO: вынести ID Ильи в опции или найти по роли
        // Пока ищем первого пользователя с ролью Administrator
        var users = userRepository.Query().ToList();

        // Заглушка: возвращаем первый попавшийся GUID
        // В реальности нужно искать по имени/роли
        throw new InvalidOperationException("Coach (Илья) не найден. Укажите ID в настройках.");
    }

    private static string BuildYandexEmail(string yandexLogin)
        => $"{yandexLogin}@edu.centraluniversity.ru";
}
