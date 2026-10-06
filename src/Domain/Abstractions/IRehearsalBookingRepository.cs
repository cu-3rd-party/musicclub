using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.Abstractions;

public interface IRehearsalBookingRepository : IRepository<RehearsalBooking>
{
    /// <summary>
    /// Находит бронирования с указанным статусом в диапазоне дат.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetByStatusAsync(
        BookingStatus status,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Находит pending-бронирования указанного пользователя.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetPendingByRequesterAsync(
        long tgUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Находит бронирование по CalDAV URL события.
    /// </summary>
    Task<RehearsalBooking?> FindByCalDavUrlAsync(
        string calDavUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Находит предстоящие бронирования в диапазоне дат.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetUpcomingAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Находит бронирования для конкретного пользователя-инициатора в диапазоне.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetByRequesterAsync(
        long tgUserId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Находит все pending-бронирования, ожидающие подтверждения Ильёй.
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetPendingForCoachAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Находит все бронирования с указанным статусом (без фильтрации по дате).
    /// </summary>
    Task<IReadOnlyList<RehearsalBooking>> GetAllByStatusAsync(
        BookingStatus status,
        CancellationToken ct = default);
}
