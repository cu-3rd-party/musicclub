using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Get_NonExistent_ThrowsNotFound()
    {
        var user = await CreateUserAsync("reader", editOwnSongs: true);

        using var scope = new SongScope();
        var ex = await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.GetAsync(Guid.NewGuid(), user, CancellationToken.None));
        ex.Message.ShouldContain("Song");
    }

    [Test]
    public async Task Get_ReturnsSongRolesAndAssignments()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var member = await CreateUserAsync("member", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" }, createdById: owner);

        await TestApp.AddAsync(new SongRoleAssignment
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            Role = "Вокал",
            UserId = member,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, owner, CancellationToken.None);

        details.Song.Id.ShouldBe(songId);
        details.Song.AvailableRoles.ShouldBe(new[] { "Вокал" });
        details.Assignments.ShouldHaveSingleItem();
        details.Assignments[0].Role.ShouldBe("Вокал");
        details.Assignments[0].User.Id.ShouldBe(member);
        details.Assignments[0].User.DisplayName.ShouldBe("Display member");
        details.Song.AssignmentCount.ShouldBe(1);
    }

    [Test]
    public async Task Get_Owner_EditableByMeTrue()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, owner, CancellationToken.None);
        details.Song.EditableByMe.ShouldBeTrue();
    }

    [Test]
    public async Task Get_OtherUser_WithoutEditAny_EditableByMeFalse()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var other = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, other, CancellationToken.None);
        details.Song.EditableByMe.ShouldBeFalse();
    }

    [Test]
    public async Task Get_OtherUser_WithEditAny_EditableByMeTrue()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var other = await CreateUserAsync("other", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner);

        using var scope = new SongScope();
        var details = await scope.Songs.GetAsync(songId, other, CancellationToken.None);
        details.Song.EditableByMe.ShouldBeTrue();
    }
}
