using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Song;

public partial class SongService
{
    private static SongUserDto MapCreatedBy(ApplicationUser? user)
    {
        if (user is null) return new SongUserDto(Guid.Empty, "Unknown", "unknown", null);

        return new SongUserDto(user.Id,
            user.DisplayName,
            user.UserName ?? string.Empty,
            user.AvatarUrl,
            user.TgUserId);
    }

    private static SongDto ToSongDto(Domain.Entities.Song song, IReadOnlyList<SongRole> roles, ApplicationUser? roadie = null)
    {
        var roleDtos = roles
            .Select(r => new RoleDto(r.Id,
                r.RoleTitle,
                r.Assignment is null
                    ? null
                    : new RoleAssignmentDto(r.Assignment.Id,
                        new SongUserDto(r.Assignment.User.Id,
                            r.Assignment.User.DisplayName,
                            r.Assignment.User.UserName,
                            r.Assignment.User.AvatarUrl,
                            r.Assignment.User.TgUserId),
                        r.Assignment.JoinedAt)))
            .ToList();

        return new SongDto(song.Id,
            song.Title,
            song.Artist,
            song.Description,
            song.LinkUrl,
            song.ThumbnailUrl,
            song.IsFeatured,
            MapCreatedBy(song.CreatedBy),
            roleDtos,
            roadie is null
                ? null
                : new SongUserDto(roadie.Id,
                    roadie.DisplayName,
                    roadie.UserName,
                    roadie.AvatarUrl,
                    roadie.TgUserId),
            song.CreatedAt,
            song.UpdatedAt);
    }
}