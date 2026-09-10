using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace CuMusicClub.Application.Services.Song;

public partial class SongService
{
    public async Task<SongDto> JoinRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var requester = await users.FindByIdAsync(claimsPrincipal.GetUserId()) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;

        var permissions = await permissionService.GetPermissionValuesAsync(requester, cancellationToken);
        if ((isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditOwn)) ||
            (!isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditAny)))
            throw new ForbiddenAccessException();

        if (!isSelf)
        {
            var prefs = await users.GetPreferencesAsync(user.Id, cancellationToken);
            if (prefs is { AllowAdding: false })
                throw new ForbiddenAccessException();
        }

        var role = await songRoles.FindByIdWithSongAndAssignmentAsync(roleId, cancellationToken);
        if (role == null) throw new NotFoundException(roleId.ToString(), nameof(SongRole));

        if (role.Assignment != null) throw new BadHttpRequestException("the role is already occupied");

        role.Assignment = new SongRoleAssignment
        {
            UserId = user.Id,
            SongId = role.SongId,
            RoleId = role.Id,
        };
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var song = await GetAsync(role.Song.Id, cancellationToken);
        var existing = await songTopics.FindBySongIdAsync(song.Id, cancellationToken);
        if (existing == null && song.IsFull)
        {
            await roadieService.CreateTicketAsync(song.Id, requester, RoadieTicketType.Assignment, cancellationToken);
            await new SongServiceTopics(telegramChatService).CreateTopicForFullSongAsync(role.Song, cancellationToken);
        }
        else if (existing != null)
            await new SongServiceTopics(telegramChatService).AnnounceParticipantJoinAsync(existing.TopicId, user, role.RoleTitle, cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
    }

    public async Task<SongDto> LeaveRoleAsync(ApplicationUser user,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var requester = await users.FindByIdAsync(claimsPrincipal.GetUserId()) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;

        var permissions = await permissionService.GetPermissionValuesAsync(requester, cancellationToken);
        if ((isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditOwn)) ||
            (!isSelf && !permissions.Contains(Domain.Constants.Permission.ParticipationEditAny)))
            throw new ForbiddenAccessException();

        if (!isSelf)
        {
            var prefs = await users.GetPreferencesAsync(user.Id, cancellationToken);
            if (prefs is { AllowRemoving: false })
                throw new ForbiddenAccessException();
        }

        var role = await songRoles.FindByIdWithSongAndAssignmentAsync(roleId, cancellationToken);
        if (role == null) throw new NotFoundException(roleId.ToString(), nameof(SongRole));

        if (role.Assignment == null) throw new BadHttpRequestException("role is unoccupied");

        var song = await GetAsync(role.Song.Id, cancellationToken);
        var topic = await songTopics.FindBySongIdAsync(song.Id, cancellationToken);
        if (topic != null) await new SongServiceTopics(telegramChatService).AnnounceParticipantLeaveAsync(topic.TopicId, user, role.RoleTitle, cancellationToken);

        await songRoleAssignments.RemoveByIdAsync(role.Assignment.Id, cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
    }
}
