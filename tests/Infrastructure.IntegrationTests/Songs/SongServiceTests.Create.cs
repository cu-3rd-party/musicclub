using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task Create_WithoutPermission_ThrowsForbidden()
    {
        var (_, principal) = await CreateUserAsync("no-perm");

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.CreateAsync(CreateRequest(), principal, CancellationToken.None));
    }

    [Test]
    public async Task Create_WithEditOwnSongs_CreatesSongWithRoles()
    {
        var (appUser, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        SongDto result;
        using (var scope = new SongScope())
        {
            result = await scope.Songs.CreateAsync(CreateRequest(roles: new[]
                {
                    "Гитара",
                    "Вокал"
                }),
                principal,
                CancellationToken.None);

            result.Title.ShouldBe("Bohemian Rhapsody");
            result.Artist.ShouldBe("Queen");
            result.Url.ShouldBe(YoutubeUrl);
            result.CreatedBy.Id.ShouldBe(appUser.Id);
            result.Roles.Select(r => r.Title)
                .OrderBy(x => x)
                .ShouldBe(new[]
                {
                    "Вокал",
                    "Гитара"
                });
            result.Roles.Count(r => r.Assignment != null)
                .ShouldBe(0);
        }

        using var db = Db();
        (await db.Songs.CountAsync(s => s.Id == result.Id)).ShouldBe(1);
        (await db.SongRoles.CountAsync(r => r.SongId == result.Id)).ShouldBe(2);
    }

    [Test]
    public async Task Create_WithEditAnySongs_IsAllowed()
    {
        var (_, principal) = await CreateUserAsync("editor", editOwnParticipation: true, editAnySongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(), principal, CancellationToken.None);
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task Create_FeaturedWithoutPermission_ThrowsForbidden()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            scope.Songs.CreateAsync(CreateRequest(featured: true), principal, CancellationToken.None));
    }

    [Test]
    public async Task Create_FeaturedWithPermission_SetsFeatured()
    {
        var (_, principal) = await CreateUserAsync("editor",
            editOwnParticipation: true,
            editAnySongs: true,
            editFeaturedSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(featured: true), principal, CancellationToken.None);
        result.Featured.ShouldBeTrue();
    }

    [Test]
    public async Task Create_YoutubeLink_ExtractsThumbnail()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(), principal, CancellationToken.None);
        result.ThumbnailUrl.ShouldBe("https://img.youtube.com/vi/fJ9rUzIMcZQ/hqdefault.jpg");
    }

    [Test]
    public async Task Create_CustomThumbnail_OverridesExtracted()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(thumbnailUrl: "https://cdn.example.com/thumb.jpg"),
            principal,
            CancellationToken.None);
        result.ThumbnailUrl.ShouldBe("https://cdn.example.com/thumb.jpg");
    }

    [Test]
    public async Task Create_NonYoutubeLink_NoAutoThumbnail()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(url: "https://soundcloud.com/foo"),
            principal,
            CancellationToken.None);
        result.ThumbnailUrl.ShouldBeNull();
    }

    [Test]
    public async Task Create_UnsupportedLinkUrl_ThrowsValidation()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        await Should.ThrowAsync<ValidationException>(() =>
            scope.Songs.CreateAsync(CreateRequest(url: "https://vimeo.com/123"), principal, CancellationToken.None));
    }

    [Test]
    public async Task Create_NormalizesRoles_TrimsDedupesAndSorts()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(roles: new[]
            {
                "  Вокал ",
                "",
                "гитара",
                "Гитара",
                "Вокал"
            }),
            principal,
            CancellationToken.None);

        result.Roles.Select(r => r.Title)
            .ToArray()
            .ShouldBe(new[]
                {
                    "Вокал",
                    "Гитара",
                    "гитара"
                },
                ignoreOrder: true);

        using var db = Db();
        var stored = await db.SongRoles.Where(r => r.SongId == result.Id)
            .Select(r => r.RoleTitle)
            .OrderBy(r => r)
            .ToListAsync();
        stored.ShouldBe(new[]
        {
            "Вокал",
            "Гитара",
            "гитара"
        }, ignoreOrder: true);
    }

    [Test]
    public async Task Create_EmptyRoles_CreatesSongWithoutRoles()
    {
        var (_, principal) = await CreateUserAsync("owner", editOwnParticipation: true, editOwnSongs: true);

        using var scope = new SongScope();
        var result = await scope.Songs.CreateAsync(CreateRequest(roles: []), principal, CancellationToken.None);
        result.Roles.ShouldBeEmpty();
    }
}
