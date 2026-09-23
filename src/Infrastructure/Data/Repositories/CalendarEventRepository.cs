using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public class CalendarEventRepository : Repository<CalendarEvent>, ICalendarEventRepository
{
    public CalendarEventRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IEnumerable<CalendarEvent>> GetUserEventsAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await Query()
            .Where(e => e.UserId == userId && e.StartAt >= from && e.EndAt <= to)
            .OrderBy(e => e.StartAt)
            .ToListAsync(ct);
    }
    
    public async Task<IEnumerable<CalendarEvent>> GetUserActiveEventsAsync(Guid userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await Query()
            .Where(e => e.UserId == userId && e.DeletedAt == null && e.StartAt >= from && e.EndAt <= to)
            .OrderBy(e => e.StartAt)
            .ToListAsync(ct);
    }
    
    public async Task<CalendarEvent?> FindBySourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default)
    {
        return await Query()
            .FirstOrDefaultAsync(e => e.SourceType == sourceType && e.SourceId == sourceId, ct);
    }
    
    public async Task<IEnumerable<CalendarEvent>> FindBySourceAndUserAsync(string sourceType, Guid sourceId, Guid userId, CancellationToken ct = default)
    {
        return await Query()
            .Where(e => e.SourceType == sourceType && e.SourceId == sourceId && e.UserId == userId)
            .ToListAsync(ct);
    }
}
