using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public class RehearsalBookingRepository : Repository<RehearsalBooking>, IRehearsalBookingRepository
{
    public RehearsalBookingRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetByStatusAsync(
        BookingStatus status,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        from = ToUtc(from);
        to = ToUtc(to);

        return await Query()
            .Where(b => b.Status == status
                     && b.ScheduledAt >= from
                     && b.ScheduledAt <= to)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetPendingByRequesterAsync(
        long tgUserId,
        CancellationToken ct = default)
    {
        return await Query()
            .Where(b => b.RequesterTgUserId == tgUserId && b.Status == BookingStatus.Pending)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<RehearsalBooking?> FindByCalDavUrlAsync(
        string calDavUrl,
        CancellationToken ct = default)
    {
        return await Query()
            .FirstOrDefaultAsync(b => b.CalDavEventUrl == calDavUrl, ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetUpcomingAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        from = ToUtc(from);
        to = ToUtc(to);

        return await Query()
            .Where(b => b.ScheduledAt >= from
                     && b.ScheduledAt <= to
                     && b.Status == BookingStatus.Confirmed)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetByRequesterAsync(
        long tgUserId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        from = ToUtc(from);
        to = ToUtc(to);

        return await Query()
            .Where(b => b.RequesterTgUserId == tgUserId
                     && b.ScheduledAt >= from
                     && b.ScheduledAt <= to)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetPendingForRoadieAsync(
        CancellationToken ct = default)
    {
        return await Query()
            .Where(b => b.Status == BookingStatus.Pending)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetAllByStatusAsync(
        BookingStatus status,
        CancellationToken ct = default)
    {
        return await Query()
            .Where(b => b.Status == status)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RehearsalBooking>> GetActiveInRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        return await Query()
            .Where(b => b.Status != BookingStatus.Rejected
                     && b.Status != BookingStatus.Cancelled
                     && b.ScheduledAt >= from
                     && b.ScheduledAt < to)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Npgsql пишет в timestamptz только UTC; Unspecified трактуем как МСК (см. DateExtensions).
    /// </summary>
    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => value.FromMskToUtc(),
            _ => value
        };
    }
}
