using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class SongRoleAssignmentRepository(DbContext dbContext)
    : Repository<SongRoleAssignment>(dbContext), ISongRoleAssignmentRepository
{
    public async Task RemoveByIdAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await DbSet.Where(s => s.Id == assignmentId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetMemberUserIdsBySongIdAsync(Guid songId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.SongId == songId)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSongIdsByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(a => a.UserId == userId)
            .Select(a => a.SongId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
