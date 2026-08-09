using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Song;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Create_WithoutPermission_ThrowsForbidden()
    {
        var user = await CreateUserAsync("no-perm");

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.CreateAsync(CreateRequest(), user, CancellationToken.None));
    }

    [Test]
    public async Task Create_WithEditOwnSongs_CreatesSongWithRoles()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        Guid songId;
        using (var scope = new SongScope())
        {
            var details = await scope.Songs.CreateAsync(
                CreateRequest(roles: new[] { "Гитара", "Вокал" }), user, CancellationToken.None);
            songId = details.Song.Id;

            details.Song.Title.ShouldBe("Bohemian Rhapsody");
            details.Song.Artist.ShouldBe("Queen");
            details.Song.Url.ShouldBe(YoutubeUrl);
            details.Song.CreatedBy.Id.ShouldBe(user.GetUserId());
            details.Song.Roles.Select(r => r.Title).ShouldBe(new[] { "Вокал", "Гитара" });
            details.Song.EditableByMe.ShouldBeTrue();
            details.Song.AssignmentCount.ShouldBe(0);
            details.Permissions.EditOwnSongs.ShouldBeTrue();
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == songId)).ShouldBe(1);
        (await db.SongRoles.CountAsync(r => r.SongId == songId)).ShouldBe(2);
    }

    [Test]
    public async Task Create_WithEditAnySongs_IsAllowed()
    {
        var user = await CreateUserAsync("editor", editAnySongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(CreateRequest(), user, CancellationToken.None);
        details.Song.EditableByMe.ShouldBeTrue();
    }

    [Test]
    public async Task Create_FeaturedWithoutPermission_ThrowsForbidden()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.CreateAsync(CreateRequest(featured: true), user, CancellationToken.None));
    }

    [Test]
    public async Task Create_FeaturedWithPermission_SetsFeatured()
    {
        var user = await CreateUserAsync("editor", editAnySongs: true, editFeaturedSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(CreateRequest(featured: true), user, CancellationToken.None);
        details.Song.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Create_YoutubeLink_ExtractsThumbnail()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(CreateRequest(), user, CancellationToken.None);
        details.Song.ThumbnailUrl.ShouldBe("https://img.youtube.com/vi/fJ9rUzIMcZQ/hqdefault.jpg");
    }

    [Test]
    public async Task Create_CustomThumbnail_OverridesExtracted()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(
            CreateRequest(thumbnailUrl: "https://cdn.example.com/thumb.jpg"), user, CancellationToken.None);
        details.Song.ThumbnailUrl.ShouldBe("https://cdn.example.com/thumb.jpg");
    }

    [Test]
    public async Task Create_NonYoutubeLink_NoAutoThumbnail()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(
            CreateRequest(url: "https://soundcloud.com/foo"), user, CancellationToken.None);
        details.Song.ThumbnailUrl.ShouldBeNull();
    }

    [Test]
    public async Task Create_UnsupportedLinkUrl_ThrowsValidation()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<ValidationException>(() =>
            scope.Songs.CreateAsync(CreateRequest(url: "https://vimeo.com/123"), user, CancellationToken.None));
    }

    [Test]
    public async Task Create_NormalizesRoles_TrimsDedupesAndSorts()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(
            CreateRequest(roles: new[] { "  Вокал ", "", "гитара", "Гитара", "Вокал" }),
            user,
            CancellationToken.None);

        details.Song.Roles.Select(r => r.Title).ShouldBe(new[] { "Вокал", "Гитара", "гитара" });

        using var db = Db();
        var stored = await db.SongRoles
            .Where(r => r.SongId == details.Song.Id)
            .Select(r => r.Role)
            .OrderBy(r => r)
            .ToListAsync();
        stored.ShouldBe(new[] { "Вокал", "Гитара", "гитара" });
    }

    [Test]
    public async Task Create_EmptyRoles_CreatesSongWithoutRoles()
    {
        var user = await CreateUserAsync("owner", editOwnSongs: true);

        using var scope = new SongScope();
        var details = await scope.Songs.CreateAsync(CreateRequest(roles: []), user, CancellationToken.None);
        details.Song.Roles.ShouldBeEmpty();
    }
}
