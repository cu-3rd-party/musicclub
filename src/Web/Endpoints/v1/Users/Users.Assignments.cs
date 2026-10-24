using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Constants;

namespace CuMusicClub.Web.Endpoints.v1.Users;

public static partial class Users
{
    public static async Task<IReadOnlyList<SongRoleAssignment>> GetMyAssignments(
        IApplicationUserRepository users,
        ISongRoleAssignmentRepository songRoleAssignmentRepository,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(claimsPrincipal.GetUserId()) ?? throw new UnauthorizedAccessException();

        var assignments = await songRoleAssignmentRepository
            .Query()
            .Where(a => a.UserId == user.Id)
            .Include(a => a.Song)
            .Include(a => a.SongRole)
            .Select(a => new SongRoleAssignment(a.Id,
                new ShortSongDto(a.Song.Id, a.Song.Title, a.Song.Artist, a.Song.ThumbnailUrl),
                a.SongRole.RoleTitle,
                a.JoinedAt,
                a.Song.Assignments
                    .Select(sa => new SongUserDto(sa.User.Id, sa.User.DisplayName, sa.User.UserName, sa.User.AvatarUrl, null))
                    .ToList()))
            .ToListAsync(cancellationToken);

        // один человек может занимать несколько ролей в песне — в составе он должен быть один раз
        return assignments
            .Select(a => a with { Participants = a.Participants.DistinctBy(p => p.Id).ToList() })
            .ToList();
    }

    public static async Task<IReadOnlyList<ShortSongDto>> GetMyRoadieAssignments(
        IApplicationUserRepository users,
        ISongRoleAssignmentRepository songRoleAssignmentRepository,
        ISongRoadieRepository songRoadieRepository,
        IRoadieService roadieService,
        IPermissionService permissionService,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(claimsPrincipal.GetUserId()) ?? throw new UnauthorizedAccessException();
        var permissionValuesAsync = await permissionService.GetPermissionValuesAsync(user, cancellationToken);
        if (!permissionValuesAsync.Contains(Permission.RoadieManage)) throw new ForbiddenAccessException();

        return await songRoadieRepository.Query()
            .Where(s => s.RoadieId == user.Id)
            .Include(s => s.Song)
            .Select(a => new ShortSongDto(a.Song.Id, a.Song.Title, a.Song.Artist, a.Song.ThumbnailUrl))
            .ToListAsync(cancellationToken);
    }
}
