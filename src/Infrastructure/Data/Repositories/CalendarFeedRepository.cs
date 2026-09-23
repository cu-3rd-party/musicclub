using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public class CalendarFeedRepository : Repository<CalendarFeed>, ICalendarFeedRepository
{
    public CalendarFeedRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<CalendarFeed?> GetUserFeedAsync(Guid userId, CancellationToken ct = default)
    {
        return await Query()
            .FirstOrDefaultAsync(e => e.UserId == userId, ct);
    }
    
    public async Task<CalendarFeed?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await Query()
            .FirstOrDefaultAsync(e => e.FeedToken == token, ct);
    }
    
    public async Task<CalendarFeed> UpsertFeedAsync(CalendarFeed feed, CancellationToken ct = default)
    {
        var existing = await GetUserFeedAsync(feed.UserId, ct);
        
        if (existing != null)
        {
            existing.FeedToken = feed.FeedToken;
            existing.IsActive = feed.IsActive;
            existing.RevokedAt = feed.RevokedAt;
            
            Update(existing);
            await SaveChangesAsync(ct);
            return existing;
        }
        
        await AddAsync(feed, ct);
        await SaveChangesAsync(ct);
        return feed;
    }
}
