using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

/// <summary>Репозиторий назначений пользователей на роли.</summary>
public interface ISongRoleAssignmentRepository : IRepository<SongRoleAssignment>
{
    Task RemoveByIdAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>ID всех пользователей, назначенных на роли песни.</summary>
    Task<IReadOnlyList<Guid>> GetMemberUserIdsBySongIdAsync(Guid songId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSongIdsByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default);
}
