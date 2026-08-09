using System.Security.Claims;

namespace CuMusicClub.Application.Song;

public interface ISongService
{
    Task<ListSongsResultDto> ListAsync(string? query, int pageSize, string? pageToken, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task<SongDetailsDto> GetAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task<SongDetailsDto> CreateAsync(CreateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task<SongDetailsDto> UpdateAsync(Guid songId, UpdateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task DeleteAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task<SongDetailsDto> JoinRoleAsync(Guid songId, string role, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
    Task<SongDetailsDto> LeaveRoleAsync(Guid songId, string role, ClaimsPrincipal currentUser, CancellationToken cancellationToken);
}
