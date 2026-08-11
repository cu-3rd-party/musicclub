using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Song;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Update_NonExistent_ThrowsNotFound()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, false, null);
        await Should.ThrowAsync<NotFoundException>(() =>
            scope.Songs.UpdateAsync(Guid.NewGuid(), request, principal, CancellationToken.None));
    }

    [Test]
    public async Task Update_ByOwner_UpdatesFieldsAndRoles()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: new[]
            {
                "Вокал",
                "Гитара"
            },
            createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("Stairway to Heaven",
            "Led Zeppelin",
            "epic solo",
            "https://music.yandex.ru/album/1",
            "https://cdn.example.com/stairs.jpg",
            false,
            new[]
            {
                "Вокал",
                "Соло"
            });

        var result = await scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None);

        result.Title.ShouldBe("Stairway to Heaven");
        result.Artist.ShouldBe("Led Zeppelin");
        result.Description.ShouldBe("epic solo");
        result.Url.ShouldBe("https://music.yandex.ru/album/1");
        result.ThumbnailUrl.ShouldBe("https://cdn.example.com/stairs.jpg");
        result.Roles.Select(r => r.Title)
            .ToArray()
            .ShouldBe(new[]
                {
                    "Вокал",
                    "Соло"
                },
                ignoreOrder: true);

        using var db = Db();
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(2);
        (await db.SongRoles.CountAsync(r => r.SongId == songId && r.RoleTitle == "Гитара")).ShouldBe(0);
    }

    [Test]
    public async Task Update_ByOtherUser_WithoutEditAny_ThrowsForbidden()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (_, otherPrincipal) = await CreateUserAsync("other", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, false, null);
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.UpdateAsync(songId, request, otherPrincipal, CancellationToken.None));
    }

    [Test]
    public async Task Update_ByOtherUser_WithEditAny_Succeeds()
    {
        var (owner, _) = await CreateUserAsync("owner", editOwnSongs: true);
        var (_, editorPrincipal) = await CreateUserAsync("editor", editAnySongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, false, null);
        var result = await scope.Songs.UpdateAsync(songId, request, editorPrincipal, CancellationToken.None);

        result.Title.ShouldBe("New Title");
    }

    [Test]
    public async Task Update_FeaturedWithoutPermission_ThrowsForbidden()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, true, null);
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None));
    }

    [Test]
    public async Task Update_FeaturedWithPermission_UpdatesFeatured()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true, editFeaturedSongs: true);
        var songId = await SeedSongAsync(createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, true, null);
        var result = await scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None);

        result.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Update_WithoutFeaturedPermission_KeepsFeaturedValue()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(featured: true, createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title", "New Artist", null, YoutubeUrl, null, false, null);
        var result = await scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None);

        result.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Update_RemovingRoles_DeletesThem()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: ["Вокал", "Гитара", "Бас"], createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title",
            "New Artist",
            null,
            YoutubeUrl,
            null,
            false,
            new[]
            {
                "Вокал"
            });
        var result = await scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None);

        result.Roles.Select(r => r.Title)
            .ShouldBe(new[]
            {
                "Вокал"
            });

        using var db = Db();
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(1);
    }

    [Test]
    public async Task Update_NormalizesRoles_TrimsDedupesAndSorts()
    {
        var (owner, principal) = await CreateUserAsync("owner", editOwnSongs: true);
        var songId = await SeedSongAsync(roles: new[]
            {
                "Вокал"
            },
            createdById: owner.Id);

        using var scope = new SongScope();
        var request = new UpdateSongRequest("New Title",
            "New Artist",
            null,
            YoutubeUrl,
            null,
            false,
            new[]
            {
                "  Вокал ",
                "",
                "гитара",
                "Гитара",
                "Вокал"
            });
        var result = await scope.Songs.UpdateAsync(songId, request, principal, CancellationToken.None);

        result.Roles.Select(r => r.Title)
            .ToArray()
            .ShouldBe(new[]
                {
                    "Вокал",
                    "Гитара",
                    "гитара"
                },
                ignoreOrder: true);
    }
}
