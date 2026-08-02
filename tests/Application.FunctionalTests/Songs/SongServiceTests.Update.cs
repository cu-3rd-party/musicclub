using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Songs;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    
    #region Update

    [Test]
    public async Task Update_NonExistent_ThrowsNotFound()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false, null);
        await Should.ThrowAsync<NotFoundException>(
            () => scope.Songs.UpdateAsync(Guid.NewGuid(), request, user, CancellationToken.None));
    }

    [Test]
    public async Task Update_ByOwner_UpdatesFieldsAndRoles()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал", "Гитара" }, createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "Stairway to Heaven",
            "Led Zeppelin",
            "epic solo",
            new SongLinkDto("yandex_music", "https://music.yandex.ru/album/1"),
            "https://cdn.example.com/stairs.jpg",
            false,
            new[] { "Вокал", "Соло" });

        var details = await scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None);

        details.Song.Title.ShouldBe("Stairway to Heaven");
        details.Song.Artist.ShouldBe("Led Zeppelin");
        details.Song.Description.ShouldBe("epic solo");
        details.Song.Link.Kind.ShouldBe("yandex_music");
        details.Song.ThumbnailUrl.ShouldBe("https://cdn.example.com/stairs.jpg");
        details.Song.AvailableRoles.ShouldBe(new[] { "Вокал", "Соло" });

        using var db = Db();
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(2);
        (await db.SongRoles.CountAsync(r => r.SongId == songId && r.Role == "Гитара")).ShouldBe(0);
    }

    [Test]
    public async Task Update_ByOtherUser_WithoutEditAny_ThrowsForbidden()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var other = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false, null);
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => scope.Songs.UpdateAsync(songId, request, other, CancellationToken.None));
    }

    [Test]
    public async Task Update_ByOtherUser_WithEditAny_Succeeds()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var editor = await CreateUserAsync("editor", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false, null);
        var details = await scope.Songs.UpdateAsync(songId, request, editor, CancellationToken.None);

        details.Song.Title.ShouldBe("New Title");
        details.Song.EditableByMe.ShouldBeTrue();
    }

    [Test]
    public async Task Update_FeaturedWithoutPermission_ThrowsForbidden()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, true, null);
        await Should.ThrowAsync<ForbiddenAccessException>(
            () => scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None));
    }

    [Test]
    public async Task Update_FeaturedWithPermission_UpdatesFeatured()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true, editFeaturedSongs: true);
        var songId = await SeedSongAsync(createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, true, null);
        var details = await scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None);

        details.Song.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Update_WithoutFeaturedPermission_KeepsFeaturedValue()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(featured: true, createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false, null);
        var details = await scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None);

        details.Song.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Update_RemovingRoles_DeletesThem()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал", "Гитара", "Бас" }, createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false, new[] { "Вокал" });
        var details = await scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None);

        details.Song.AvailableRoles.ShouldBe(new[] { "Вокал" });

        using var db = Db();
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(1);
    }

    [Test]
    public async Task Update_NormalizesRoles_TrimsDedupesAndSorts()
    {
        var owner = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал" }, createdById: owner.GetUserId());

        using var scope = new SongScope();
        var request = new UpdateSongRequest(
            "New Title", "New Artist", null, new SongLinkDto("youtube", YoutubeUrl), null, false,
            new[] { "  Вокал ", "", "гитара", "Гитара", "Вокал" });
        var details = await scope.Songs.UpdateAsync(songId, request, owner, CancellationToken.None);

        details.Song.AvailableRoles.ShouldBe(new[] { "Вокал", "Гитара", "гитара" });
    }

    #endregion

}
