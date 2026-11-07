using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class SongRepository(DbContext dbContext) : Repository<Song>(dbContext), ISongRepository
{
    private const string CaseInsensitiveCollation = "und-x-icu";

    public async Task<Song?> FindByIdWithDetailsAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(s => s.CreatedBy)
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken);
    }

    public async Task<Song?> FindByIdWithCreatedByAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken);
    }

    public async Task<Song?> FindByIdWithCreatedByAndTopicAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(s => s.CreatedBy)
            .Include(song => song.SongTopic!)
            .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken);
    }

    public async Task<Song?> FindByIdWithTopicAndRolesAsync(Guid songId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(s => s.SongTopic)
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .FirstAsync(s => s.Id == songId, cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> ListSongsAsync(string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var songsQuery = DbSet
            .Include(s => s.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            // pg-grm (CNPG) создан с locale C: там ILIKE складывает регистр только у латиницы.
            // ICU-коллация даёт регистронезависимый поиск и по кириллице независимо от локали базы.
            songsQuery = songsQuery.Where(s =>
                EF.Functions.ILike(EF.Functions.Collate(s.Title, CaseInsensitiveCollation), pattern) ||
                EF.Functions.ILike(EF.Functions.Collate(s.Artist, CaseInsensitiveCollation), pattern));
        }

        return await songsQuery
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .OrderByDescending(s => s.IsFeatured)
            .ThenByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> GetSongsForThumbnailBackfillAsync(int limit,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.ThumbnailDataEntryId == null && s.ThumbnailUrl != null && s.ThumbnailUrl.Trim() != "")
            .OrderBy(s => s.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Song>> GetFilledSongIdsWithoutTopicAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(s => s.SongTopic == null && s.Roles.Count > 0 &&
                        s.Roles.Count(r => r.Assignment != null) == s.Roles.Count)
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTitlesByIdsAsync(IReadOnlyCollection<Guid> songIds,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(s => songIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Title, cancellationToken);
    }
}
