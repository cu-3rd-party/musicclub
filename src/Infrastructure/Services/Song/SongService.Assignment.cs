using System.Security.Claims;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.Song;

public partial class SongService
{
    public async Task<SongDto> JoinRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        var requester = await userManager.GetUserAsync(claimsPrincipal) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;
        if ((isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditOwn)) ||
            (!isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditAny)))
            throw new ForbiddenAccessException();

        var role = await db
            .SongRoles.Include(r => r.Song)
            .Include(r => r.Assignment)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (role == null) throw new NotFoundException(roleId.ToString(), nameof(SongRole));

        if (role.Assignment != null) throw new BadHttpRequestException("the role is already occupied");

        role.Assignment = new SongRoleAssignment
        {
            UserId = user.Id,
            SongId = role.SongId,
            RoleId = role.Id,
        };
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(role.Song.Id, cancellationToken);
    }

    public async Task<SongDto> LeaveRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        var requester = await userManager.GetUserAsync(claimsPrincipal) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;
        if ((isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditOwn)) ||
            (!isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditAny)))
            throw new ForbiddenAccessException();

        var role = await db
            .SongRoles.Include(r => r.Song)
            .Include(r => r.Assignment)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (role == null) throw new NotFoundException(roleId.ToString(), nameof(SongRole));

        if (role.Assignment == null) throw new BadHttpRequestException("role is unoccupied");

        await db
            .SongRoleAssignments.Where(s => s.Id == role.Assignment.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetAsync(role.Song.Id, cancellationToken);
    }
}
