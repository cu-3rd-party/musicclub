using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Get_NonExistent_ThrowsNotFound()
    {
        var (_, principal) = await CreateUserAsync("reader", editOwnSongs: true);

        using var scope = new SongScope();
        var ex = await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.GetAsync(Guid.NewGuid(), CancellationToken.None));
        ex.Message.ShouldContain("Song");
    }

    [Test]
    public async Task Get_ReturnsSongRolesAndAssignments()
    {
        var (owner, ownerPrincipal) = await CreateUserAsync("owner", editOwnSongs: true);
        var (member, _) = await CreateUserAsync("member", true);
        var songId = await SeedSongAsync(roles: new[]
            {
                "Вокал",
            },
            createdById: owner.Id);

        await SeedAssignmentAsync(songId, "Вокал", member.Id);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, CancellationToken.None);

        details.Id.ShouldBe(songId);
        details
            .Roles.Select(r => r.Title)
            .ShouldBe(new[]
            {
                "Вокал",
            });
        details.Roles.ShouldHaveSingleItem();
        details
            .Roles[0]
            .Assignment.ShouldNotBeNull();
        details.Roles[0].Assignment!.User.Id.ShouldBe(member.Id);
        details.Roles[0].Assignment!.User.DisplayName.ShouldBe("Display member");
    }

    [Test]
    public async Task Get_Owner_EditableByMeTrue()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, CancellationToken.None);
        details.ShouldNotBeNull();
    }

    [Test]
    public async Task Get_OtherUser_WithoutEditAny_CanRead()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (other, _) = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, CancellationToken.None);
        details.Id.ShouldBe(songId);
    }

    [Test]
    public async Task Get_OtherUser_WithEditAny_CanRead()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (other, _) = await CreateUserAsync("other", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, CancellationToken.None);
        details.Id.ShouldBe(songId);
    }
}
