using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Join_WithoutParticipationPermission_ThrowsForbidden()
    {
        var user = await CreateUserAsync("user");
        var songId = await SeedSongAsync(roles: new[] { "Вокал" });

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => scope.Songs.JoinRoleAsync(songId, "Вокал", user, CancellationToken.None));
    }

    [Test]
    public async Task Join_AddsAssignment()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал", "Гитара" });

        using var scope = new SongScope();
        var details = await scope.Songs.JoinRoleAsync(songId, "Вокал", user, CancellationToken.None);

        details.Song.AssignmentCount.ShouldBe(1);
        details.Assignments.ShouldHaveSingleItem();
        details.Assignments[0].Role.ShouldBe("Вокал");
        details.Assignments[0].User.Id.ShouldBe(user);

        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(
            a => a.SongId == songId && a.Role == "Вокал" && a.UserId == user)).ShouldBe(1);
    }

    [Test]
    public async Task Join_SameRoleTwice_IsIdempotent()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" });

        using var scope = new SongScope();
        await scope.Songs.JoinRoleAsync(songId, "Вокал", user, CancellationToken.None);
        var details = await scope.Songs.JoinRoleAsync(songId, "Вокал", user, CancellationToken.None);

        details.Assignments.ShouldHaveSingleItem();
        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId && a.UserId == user)).ShouldBe(1);
    }

    [Test]
    public async Task Join_NonExistentSong_ThrowsNotFound()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(
            () => scope.Songs.JoinRoleAsync(Guid.NewGuid(), "Вокал", user, CancellationToken.None));
    }

    [Test]
    public async Task Leave_WithoutParticipationPermission_ThrowsForbidden()
    {
        var user = await CreateUserAsync("user");
        var songId = await SeedSongAsync(roles: new[] { "Вокал" });

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => scope.Songs.LeaveRoleAsync(songId, "Вокал", user, CancellationToken.None));
    }

    [Test]
    public async Task Leave_RemovesAssignment()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" });
        await TestApp.AddAsync(new SongRoleAssignment
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            Role = "Вокал",
            UserId = user,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        using (var scope = new SongScope())
        {
            var details = await scope.Songs.LeaveRoleAsync(songId, "Вокал", user, CancellationToken.None);
            details.Assignments.ShouldBeEmpty();
            details.Song.AssignmentCount.ShouldBe(0);
        }

        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId && a.UserId == user)).ShouldBe(0);
    }

    [Test]
    public async Task Leave_NotJoined_IsNoOp()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" });

        using var scope = new SongScope();
        var details = await scope.Songs.LeaveRoleAsync(songId, "Вокал", user, CancellationToken.None);

        details.Assignments.ShouldBeEmpty();
        using var db = Db();
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId)).ShouldBe(0);
    }

    [Test]
    public async Task Leave_NonExistentSong_ThrowsNotFound()
    {
        var user = await CreateUserAsync("user", editOwnParticipation: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(
            () => scope.Songs.LeaveRoleAsync(Guid.NewGuid(), "Вокал", user, CancellationToken.None));
    }
    
}
