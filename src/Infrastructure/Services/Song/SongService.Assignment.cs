using System.Security.Claims;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

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

        var song = await GetAsync(role.Song.Id, cancellationToken);
        var existing = await db.SongTopics.FirstOrDefaultAsync(t => t.SongId == song.Id, cancellationToken);
        if (existing == null && song.IsFull)
            await new SongServiceTopics(telegramChatService).CreateTopicForFullSongAsync(role.Song, cancellationToken);
        else if (existing != null)
            await new SongServiceTopics(telegramChatService).AnnounceParticipantJoinAsync(existing.TopicId, user, role.RoleTitle, cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
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

        var song = await GetAsync(role.Song.Id, cancellationToken);
        var topic = await db.SongTopics.FirstOrDefaultAsync(t => t.SongId == song.Id, cancellationToken);
        if (topic != null) await new SongServiceTopics(telegramChatService).AnnounceParticipantLeaveAsync(topic.TopicId, user, role.RoleTitle, cancellationToken);

        await db
            .SongRoleAssignments.Where(s => s.Id == role.Assignment.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
    }
}
