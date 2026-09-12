using Ardalis.GuardClauses;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using SongRoleAssignmentEntity = CuMusicClub.Domain.Entities.SongRoleAssignment;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Song;

[TestFixture]
public class GetRoleCandidatesAsyncTests : SongServiceTests
{
    private static SongRole BuildRole(Guid songId, SongRoleAssignmentEntity? assignment = null)
    {
        return new SongRole
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            RoleTitle = "Vocal",
            Assignment = assignment,
        };
    }

    private static ApplicationUser OtherUser(bool? allowAdding = null, bool? allowRemoving = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "other",
            DisplayName = "Other User",
        };
        if (allowAdding.HasValue || allowRemoving.HasValue)
            user.Preferences = new UserPreferences
            {
                UserId = user.Id,
                AllowAdding = allowAdding ?? true,
                AllowRemoving = allowRemoving ?? true,
            };
        return user;
    }

    private async Task<RoleCandidatesDto> Act(Guid songId,
        Guid roleId,
        string? query = null,
        string? mode = null)
    {
        return await _service.GetRoleCandidatesAsync(songId,
            roleId,
            query,
            mode,
            Principal(),
            CancellationToken.None);
    }

    [Test]
    public async Task Assign_VacantRole_WithEditAny_ReturnsSelfFirstThenOthers()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditOwn,
            Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var role = BuildRole(songId);
        var other1 = OtherUser();
        var other2 = OtherUser();
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other2, other1,]);

        var result = await Act(songId, role.Id);

        result.Users.Select(u => u.Id).First().ShouldBe(_userId);
        result.Users.Select(u => u.Id).ShouldContain(other1.Id);
        result.Users.Select(u => u.Id).ShouldContain(other2.Id);
    }

    [Test]
    public async Task Assign_OnlyEditOwn_ReturnsOnlySelf()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditOwn);
        var songId = Guid.NewGuid();
        var role = BuildRole(songId);
        var other = OtherUser();
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other,]);

        var result = await Act(songId, role.Id);

        result.Users.Count.ShouldBe(1);
        result.Users.Single().Id.ShouldBe(_userId);
    }

    [Test]
    public async Task Assign_ExcludesSongMembers()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var role = BuildRole(songId);
        var member = OtherUser();
        var free = OtherUser();
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([member.Id,]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([member, free,]);

        var result = await Act(songId, role.Id);

        result.Users.Select(u => u.Id).ShouldNotContain(member.Id);
        result.Users.Select(u => u.Id).ShouldContain(free.Id);
    }

    [Test]
    public async Task Assign_ExcludesUserWithAllowAddingDisabled()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var role = BuildRole(songId);
        var disabled = OtherUser(allowAdding: false);
        var allowed = OtherUser(allowAdding: true);
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([disabled, allowed,]);

        var result = await Act(songId, role.Id);

        result.Users.Select(u => u.Id).ShouldNotContain(disabled.Id);
        result.Users.Select(u => u.Id).ShouldContain(allowed.Id);
    }

    [Test]
    public async Task Assign_OccupiedRole_ReturnsEmpty()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var role = BuildRole(songId,
            new SongRoleAssignmentEntity
            {
                Id = Guid.NewGuid(),
                SongId = songId,
                RoleId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
            });
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await Act(songId, role.Id);

        result.Users.ShouldBeEmpty();
    }

    [Test]
    public async Task Assign_MissingRole_ThrowsNotFound()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SongRole?)null);

        await Should.ThrowAsync<NotFoundException>(() => Act(songId, roleId));
    }

    [Test]
    public async Task Assign_RoleFromAnotherSong_ThrowsNotFound()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var role = BuildRole(Guid.NewGuid());
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        await Should.ThrowAsync<NotFoundException>(() => Act(Guid.NewGuid(), role.Id));
    }

    [Test]
    public async Task Assign_NoPermissions_ReturnsEmpty()
    {
        CurrentUser();
        var songId = Guid.NewGuid();
        var role = BuildRole(songId);
        _songRoles
            .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await Act(songId, role.Id);

        result.Users.ShouldBeEmpty();
    }

    [Test]
    public async Task Remove_ReturnsRemovableMembers_SelfFirst()
    {
        var me = CurrentUser(Domain.Constants.Permission.ParticipationEditOwn,
            Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var other = OtherUser(allowRemoving: true);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_userId, other.Id,]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other, me,]);

        var result = await Act(songId, Guid.NewGuid(), mode: "remove");

        result.Users.Select(u => u.Id).First().ShouldBe(_userId);
        result.Users.Select(u => u.Id).ShouldContain(other.Id);
    }

    [Test]
    public async Task Remove_WithoutEditAny_ExcludesOthers()
    {
        var me = CurrentUser(Domain.Constants.Permission.ParticipationEditOwn);
        var songId = Guid.NewGuid();
        var other = OtherUser(allowRemoving: true);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_userId, other.Id,]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other, me,]);

        var result = await Act(songId, Guid.NewGuid(), mode: "remove");

        result.Users.Count.ShouldBe(1);
        result.Users.Single().Id.ShouldBe(_userId);
    }

    [Test]
    public async Task Remove_ExcludesMemberWithAllowRemovingDisabled()
    {
        CurrentUser(Domain.Constants.Permission.ParticipationEditAny);
        var songId = Guid.NewGuid();
        var blocked = OtherUser(allowRemoving: false);
        var removable = OtherUser(allowRemoving: true);
        _songRoleAssignments
            .Setup(r => r.GetMemberUserIdsBySongIdAsync(songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([blocked.Id, removable.Id,]);
        _users
            .Setup(u => u.GetUsersAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([blocked, removable,]);

        var result = await Act(songId, Guid.NewGuid(), mode: "remove");

        result.Users.Select(u => u.Id).ShouldNotContain(blocked.Id);
        result.Users.Select(u => u.Id).ShouldContain(removable.Id);
    }
}