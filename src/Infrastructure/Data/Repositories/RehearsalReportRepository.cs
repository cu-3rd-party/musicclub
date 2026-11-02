using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class RehearsalReportRepository(DbContext dbContext)
    : Repository<RehearsalReport>(dbContext), IRehearsalReportRepository
{
    public async Task<RehearsalReport?> FindByPeriodStartAsync(DateOnly periodStart, CancellationToken ct = default)
    {
        return await DbSet
            .Include(r => r.Deliveries)
            .FirstOrDefaultAsync(r => r.PeriodStart == periodStart, ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlySet<string>>> GetDeliveredRecipientsAsync(
        CancellationToken ct = default)
    {
        var deliveries = await DbContext.Set<RehearsalReportDelivery>()
            .AsNoTracking()
            .Select(d => new { d.Report.PeriodStart, d.Recipient })
            .ToListAsync(ct);

        return deliveries
            .GroupBy(d => d.PeriodStart)
            .ToDictionary(
                g => g.Key,
                IReadOnlySet<string> (g) => g.Select(d => d.Recipient).ToHashSet(StringComparer.Ordinal));
    }

    public async Task AddDeliveryAsync(RehearsalReportDelivery delivery, CancellationToken ct = default)
    {
        await DbContext.Set<RehearsalReportDelivery>().AddAsync(delivery, ct);
    }
}
