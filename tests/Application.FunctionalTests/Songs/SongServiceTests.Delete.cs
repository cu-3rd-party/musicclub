using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Delete_NonExistent_ThrowsNotFound()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(
            () => scope.Songs.DeleteAsync(Guid.NewGuid(), user, CancellationToken.None));
    }

    [Test]
    public async Task Delete_ByOwner_RemovesSongAndCascades()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var member = await CreateUserAsync("member", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" }, createdById: owner.GetUserId());
        await TestApp.AddAsync(new SongRoleAssignment
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            Role = "Вокал",
            UserId = member.GetUserId(),
            JoinedAt = DateTimeOffset.UtcNow,
        });

        using (var scope = new SongScope())
        {
            await scope.Songs.DeleteAsync(songId, owner, CancellationToken.None);
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(0);
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(0);
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId)).ShouldBe(0);
    }

    [Test]
    public async Task Delete_ByOtherUser_WithoutEditAny_ThrowsForbidden()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var other = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => scope.Songs.DeleteAsync(songId, other, CancellationToken.None));

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(1);
    }

    [Test]
    public async Task Delete_ByOtherUser_WithEditAny_Succeeds()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var editor = await CreateUserAsync("editor", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using (var scope = new SongScope())
        {
            await scope.Songs.DeleteAsync(songId, editor, CancellationToken.None);
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(0);
    }
}
