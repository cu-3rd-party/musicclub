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
        var (_, principal) = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.DeleteAsync(Guid.NewGuid(), principal, CancellationToken.None));
    }

    [Test]
    public async Task Delete_ByOwner_RemovesSongAndCascades()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var (member, _) = await CreateUserAsync("member", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[]
            {
                "Вокал"
            },
            createdById: owner.Id);
        await SeedAssignmentAsync(songId, "Вокал", member.Id);

        using (var scope = new SongScope())
        {
            await scope.Songs.DeleteAsync(songId, principal, CancellationToken.None);
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(0);
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(0);
        (await db.SongRoleAssignments.CountAsync(a => a.SongId == songId)).ShouldBe(0);
    }

    [Test]
    public async Task Delete_ByOtherUser_WithoutEditAny_ThrowsForbidden()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (_, otherPrincipal) = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.DeleteAsync(songId, otherPrincipal, CancellationToken.None));

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(1);
    }

    [Test]
    public async Task Delete_ByOtherUser_WithEditAny_Succeeds()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (_, editorPrincipal) = await CreateUserAsync("editor", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using (var scope = new SongScope())
        {
            await scope.Songs.DeleteAsync(songId, editorPrincipal, CancellationToken.None);
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(0);
    }
}
