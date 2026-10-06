using CuMusicClub.Application.Common.Exceptions;
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
    /// Создаёт бронирование репетиции (confirmed, без запроса к Илье).
    /// </summary>
    /// <exception cref="BookingRuleException">
    /// Пользователь не участник песни, время прошло, слишком далеко или зал уже забронирован.
    /// </exception>
    Task<RehearsalBooking> CreateBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = 80,
        CancellationToken ct = default);

    /// <summary>
    /// Создаёт бронирование с запросом к роуди (pending).
    /// </summary>
    Task<RehearsalBooking> CreateBookingWithRoadieAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = 80,
        CancellationToken ct = default);

    /// <summary>
    /// Подтверждает pending-бронирование. Нужно право <see cref="Domain.Constants.Permission.EventsEdit"/>.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">У подтверждающего нет права.</exception>
    Task<RehearsalBooking> ApproveBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Отклоняет pending-бронирование. Нужно право <see cref="Domain.Constants.Permission.EventsEdit"/>.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">У отклоняющего нет права.</exception>
    Task<RehearsalBooking> RejectBookingAsync(
        Guid bookingId,
        long approverTgUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Может ли пользователь Telegram подтверждать и отклонять бронирования.
    /// </summary>
    Task<bool> CanApproveAsync(long tgUserId, CancellationToken ct = default);

    /// <summary>
    /// Отменяет подтверждённое или ожидающее бронирование инициатором.
    /// </summary>
    Task<RehearsalBooking> CancelBookingAsync(
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
    private const int MaxDaysAhead = 60;
    private const int MaxDurationMinutes = 240;
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);

    public async Task<RehearsalBooking> CreateBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = DefaultDurationMinutes,
        CancellationToken ct = default)
    {
        await EnsureCanBookAsync(requesterTgUserId, songId, ct);
        var booking = NewBooking(requesterTgUserId, scheduledAt, songId, durationMinutes, BookingStatus.Confirmed);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        await EnsureRoomIsFreeAsync(booking, ct);
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

    public async Task<RehearsalBooking> CreateBookingWithRoadieAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes = DefaultDurationMinutes,
        CancellationToken ct = default)
    {
        // Роуди определяется по назначенному роуди песни: заявку подтверждает роуди или любой с правом events.edit
        await EnsureCanBookAsync(requesterTgUserId, songId, ct);
        var booking = NewBooking(requesterTgUserId, scheduledAt, songId, durationMinutes, BookingStatus.Pending);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        await EnsureRoomIsFreeAsync(booking, ct);
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
            ?? throw new BookingRuleException("Заявка не найдена.");

        await EnsureCanApproveAsync(approverTgUserId, ct);

        if (booking.Status != BookingStatus.Pending)
            throw new BookingRuleException($"Заявка уже {DescribeStatus(booking.Status)}.");

        await EnsureRoomIsFreeAsync(booking, ct);

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
            ?? throw new BookingRuleException("Заявка не найдена.");

        await EnsureCanApproveAsync(approverTgUserId, ct);

        if (booking.Status != BookingStatus.Pending)
            throw new BookingRuleException($"Заявка уже {DescribeStatus(booking.Status)}.");

        booking.Status = BookingStatus.Rejected;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        bookingRepository.Update(booking);
        await unitOfWork.SaveChangesAsync(ct);

        return booking;
    }

    public async Task<bool> CanApproveAsync(long tgUserId, CancellationToken ct = default)
    {
        var user = await userRepository.FindByTgUserIdAsync(tgUserId, ct);
        if (user == null) return false;

        var permissions = await userRepository.GetPermissionsAsync(user.Id, ct);
        return permissions.Contains(Domain.Constants.Permission.EventsEdit);
    }

    public async Task<RehearsalBooking> CancelBookingAsync(
        long requesterTgUserId,
        DateTime scheduledAt,
        CancellationToken ct = default)
    {
        var bookings = await bookingRepository.GetByRequesterAsync(
            requesterTgUserId,
            scheduledAt.Date,
            scheduledAt.Date.AddDays(1),
            ct);

        // В БД время в UTC, пользователь вводит МСК
        var matching = bookings.FirstOrDefault(b =>
            b.ScheduledAt.ToOffset(Msk).Hour == scheduledAt.Hour
            && b.ScheduledAt.ToOffset(Msk).Minute == scheduledAt.Minute
            && b.Status is BookingStatus.Confirmed or BookingStatus.Pending);

        if (matching == null)
            throw new BookingRuleException("У вас нет брони на это время.");

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
        return matching;
    }

    public async Task<IReadOnlyList<UserWithYandex>> GetMembersWithYandexAsync(
        Guid songId,
        CancellationToken ct = default)
    {
        var memberUserIds = (await songRoleAssignmentRepository
            .GetMemberUserIdsBySongIdAsync(songId, ct)).ToList();

        var queryable = userRepository.Query()
            .Where(u => memberUserIds.Contains(u.Id) && !string.IsNullOrEmpty(u.YandexLogin));

        var users = await queryable.ToListAsync(ct);

        var usersWithYandex = users.Select(user => new UserWithYandex(
            user.Id,
            user.DisplayName,
            user.YandexLogin!,
            BuildYandexEmail(user.YandexLogin))).ToList();

        return usersWithYandex;
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetUpcomingBookingsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        return await bookingRepository.GetUpcomingAsync(from, to, ct);
    }

    private static RehearsalBooking NewBooking(
        long requesterTgUserId,
        DateTime scheduledAt,
        Guid? songId,
        int durationMinutes,
        BookingStatus status)
    {
        var start = new DateTimeOffset(DateTime.SpecifyKind(scheduledAt, DateTimeKind.Unspecified), Msk)
            .ToUniversalTime();
        var now = DateTimeOffset.UtcNow;

        if (durationMinutes is <= 0 or > MaxDurationMinutes)
            throw new BookingRuleException($"Длительность должна быть от 1 до {MaxDurationMinutes} минут.");
        if (start < now)
            throw new BookingRuleException("Это время уже прошло.");
        if (start > now.AddDays(MaxDaysAhead))
            throw new BookingRuleException($"Бронировать можно не дальше чем на {MaxDaysAhead} дней вперёд.");

        return new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            ScheduledAt = start,
            DurationMinutes = durationMinutes,
            Status = status,
            RequesterTgUserId = requesterTgUserId,
            SongId = songId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Зал один: активные брони (подтверждённые и ожидающие) не должны пересекаться.
    /// </summary>
    private async Task EnsureRoomIsFreeAsync(RehearsalBooking booking, CancellationToken ct)
    {
        var start = booking.ScheduledAt;
        var end = start.AddMinutes(booking.DurationMinutes);

        var overlapping = (await bookingRepository.GetActiveInRangeAsync(
                start.AddMinutes(-MaxDurationMinutes), end, ct))
            .FirstOrDefault(b => b.Id != booking.Id
                                 && b.ScheduledAt < end
                                 && b.ScheduledAt.AddMinutes(b.DurationMinutes) > start);

        if (overlapping != null)
        {
            var from = overlapping.ScheduledAt.ToOffset(Msk);
            var to = from.AddMinutes(overlapping.DurationMinutes);
            var kind = overlapping.Status == BookingStatus.Pending ? "заявка" : "бронь";
            throw new BookingRuleException($"Зал уже занят: {kind} {from:dd.MM HH:mm}–{to:HH:mm}.");
        }
    }

    /// <summary>
    /// Бронировать зал под песню могут её участники и организаторы.
    /// </summary>
    private async Task EnsureCanBookAsync(long tgUserId, Guid? songId, CancellationToken ct)
    {
        var user = await userRepository.FindByTgUserIdAsync(tgUserId, ct)
                   ?? throw new BookingRuleException(
                       "Сначала откройте приложение Music Club через /start — я вас пока не знаю.");

        var permissions = await userRepository.GetPermissionsAsync(user.Id, ct);
        if (permissions.Contains(Domain.Constants.Permission.EventsEdit))
            return;

        if (songId is not { } id)
            throw new ForbiddenAccessException();

        var members = await songRoleAssignmentRepository.GetMemberUserIdsBySongIdAsync(id, ct);
        if (!members.Contains(user.Id))
            throw new BookingRuleException("Бронировать зал под песню могут только её участники.");
    }

    private async Task EnsureCanApproveAsync(long tgUserId, CancellationToken ct)
    {
        if (!await CanApproveAsync(tgUserId, ct))
            throw new ForbiddenAccessException();
    }

    private static string DescribeStatus(BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Confirmed => "подтверждена",
            BookingStatus.Rejected => "отклонена",
            BookingStatus.Cancelled => "отменена",
            _ => "обработана"
        };
    }

    private static string BuildYandexEmail(string yandexLogin)
        => $"{yandexLogin}@edu.centraluniversity.ru";
}
