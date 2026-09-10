using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class SongRoadieRepository(DbContext dbContext)
    : Repository<SongRoadie>(dbContext), ISongRoadieRepository
{
    public async Task<SongRoadie?> FindBySongIdAsync(Guid songId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(r => r.Roadie)
            .FirstOrDefaultAsync(r => r.SongId == songId, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAssembledSongIdsWithoutRoadieAsync(CancellationToken ct = default)
    {
        var songs = DbContext.Set<Song>();
        var topics = DbContext.Set<SongTopic>();
        var roadies = DbContext.Set<SongRoadie>();
        var tickets = DbContext.Set<RoadieTicket>();
        var openTicketSongIds = tickets
            .Where(t => t.AcceptedById == null)
            .Select(t => t.SongId);

        return await songs
            .Where(s => topics.Any(tp => tp.SongId == s.Id))       // есть топик => «собранная»
            .Where(s => !roadies.Any(r => r.SongId == s.Id))       // нет закреплённого роуди
            .Where(s => !openTicketSongIds.Contains(s.Id))         // нет открытой заявки
            .Select(s => s.Id)
            .ToListAsync(ct);
    }
}