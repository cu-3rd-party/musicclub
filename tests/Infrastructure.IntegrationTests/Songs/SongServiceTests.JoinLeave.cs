using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Join_WithoutParticipationPermission_ThrowsForbidden()
    {
        var (appUser, principal) = await CreateUserAsync("user");
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.JoinRoleAsync(appUser, principal, roleId, CancellationToken.None));
    }

    [Test]
    public async Task Join_AddsAssignment()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал",
            "Гитара"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");

        using var scope = new SongScope();
        var result = await scope.Songs.JoinRoleAsync(appUser, principal, roleId, CancellationToken.None);

        result.Roles.Count(r => r.Assignment != null)
            .ShouldBe(1);
        result.Roles.Single(r => r.Title == "Вокал")
            .Assignment.ShouldNotBeNull();
        result.Roles.Single(r => r.Title == "Вокал")
            .Assignment!.User.Id.ShouldBe(appUser.Id);

        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(a =>
            a.SongId == songId && a.RoleId == roleId && a.UserId == appUser.Id)).ShouldBe(1);
    }

    [Test]
    public async Task Join_SameRoleTwice_ThrowsAlreadyOccupied()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");

        using var scope = new SongScope();
        await scope.Songs.JoinRoleAsync(appUser, principal, roleId, CancellationToken.None);
        await Should.ThrowAsync<BadHttpRequestException>(() =>
            scope.Songs.JoinRoleAsync(appUser, principal, roleId, CancellationToken.None));
    }

    [Test]
    public async Task Join_NonExistentRole_ThrowsNotFound()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.JoinRoleAsync(appUser, principal, Guid.NewGuid(), CancellationToken.None));
    }

    [Test]
    public async Task Leave_WithoutParticipationPermission_ThrowsForbidden()
    {
        var (appUser, principal) = await CreateUserAsync("user");
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.LeaveRoleAsync(appUser, principal, roleId, CancellationToken.None));
    }

    [Test]
    public async Task Leave_RemovesAssignment()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");
        await SeedAssignmentAsync(songId, "Вокал", appUser.Id);

        using (var scope = new SongScope())
        {
            var result = await scope.Songs.LeaveRoleAsync(appUser, principal, roleId, CancellationToken.None);
            result.Roles.Count(r => r.Assignment != null)
                .ShouldBe(0);
        }

        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId && a.UserId == appUser.Id)).ShouldBe(0);
    }

    [Test]
    public async Task Leave_NotJoined_ThrowsRoleUnoccupied()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[]
        {
            "Вокал"
        });
        var roleId = await FindRoleIdAsync(songId, "Вокал");

        using var scope = new SongScope();
        await Should.ThrowAsync<BadHttpRequestException>(() =>
            scope.Songs.LeaveRoleAsync(appUser, principal, roleId, CancellationToken.None));
    }

    [Test]
    public async Task Leave_NonExistentRole_ThrowsNotFound()
    {
        var (appUser, principal) = await CreateUserAsync("user", editOwnParticipation: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.LeaveRoleAsync(appUser, principal, Guid.NewGuid(), CancellationToken.None));
    }
}
