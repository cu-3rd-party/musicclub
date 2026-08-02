namespace CuMusicClub.Application.Songs;

public interface ISongService
{
    Task<ListSongsResultDto> ListAsync(string? query, int pageSize, string? pageToken, Guid currentUserId, CancellationToken cancellationToken);
    Task<SongDetailsDto> GetAsync(Guid songId, Guid currentUserId, CancellationToken cancellationToken);
    Task<SongDetailsDto> CreateAsync(CreateSongRequest request, Guid currentUserId, CancellationToken cancellationToken);
    Task<SongDetailsDto> UpdateAsync(Guid songId, UpdateSongRequest request, Guid currentUserId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid songId, Guid currentUserId, CancellationToken cancellationToken);
    Task<SongDetailsDto> JoinRoleAsync(Guid songId, string role, Guid currentUserId, CancellationToken cancellationToken);
    Task<SongDetailsDto> LeaveRoleAsync(Guid songId, string role, Guid currentUserId, CancellationToken cancellationToken);
}
