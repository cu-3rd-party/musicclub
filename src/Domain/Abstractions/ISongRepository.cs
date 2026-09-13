using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

/// <summary>Репозиторий песен.</summary>
public interface ISongRepository : IRepository<Song>
{
    Task<Song?> FindByIdWithDetailsAsync(Guid songId, CancellationToken cancellationToken = default);

    Task<Song?> FindByIdWithCreatedByAsync(Guid songId, CancellationToken cancellationToken = default);

    Task<Song?> FindByIdWithCreatedByAndTopicAsync(Guid songId, CancellationToken cancellationToken = default);

    Task<Song?> FindByIdWithTopicAndRolesAsync(Guid songId, CancellationToken cancellationToken = default);

    /// <summary>Список песен с поиском по title/artist, сортировкой и пагинацией (создатель и роли подгружены).</summary>
    Task<IReadOnlyList<Song>> ListSongsAsync(string? query,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Песни без привязанного превью для бэкфилла миниатюр.</summary>
    Task<IReadOnlyList<Song>> GetSongsForThumbnailBackfillAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>Заполненные песни (все роли заняты) без созданного топика.</summary>
    Task<IReadOnlyList<Song>> GetFilledSongIdsWithoutTopicAsync(CancellationToken cancellationToken = default);
}