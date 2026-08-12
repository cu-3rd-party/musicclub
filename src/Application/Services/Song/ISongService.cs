using System.Security.Claims;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Song;

public interface ISongService
{
    Task<ListSongsResultDto> ListAsync(string? query,
        int pageSize,
        string? pageToken,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken);

    Task<SongDto> GetAsync(Guid songId, CancellationToken cancellationToken);

    Task<SongDto> CreateAsync(CreateSongRequest request,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken);

    Task<SongDto> UpdateAsync(Guid songId,
        UpdateSongRequest request,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken);

    Task<SongDto> JoinRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken);

    Task<SongDto> LeaveRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken);
}
