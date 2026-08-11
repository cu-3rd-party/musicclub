using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests
{
    [Test]
    public async Task List_Empty_ReturnsEmptyResult()
    {
        var (_, principal) = await CreateUserAsync("reader");

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync(null, 20, null, principal, CancellationToken.None);

        result.Songs.ShouldBeEmpty();
        result.NextPageToken.ShouldBeNull();
    }

    [Test]
    public async Task List_FeaturedFirst_ThenNewest()
    {
        var (_, principal) = await CreateUserAsync("reader");
        var now = DateTimeOffset.UtcNow;
        var oldId = await SeedSongAsync("Old", "A", createdAt: now.AddHours(-2));
        var featuredId = await SeedSongAsync("Featured", "B", featured: true, createdAt: now.AddHours(-1));
        var newestId = await SeedSongAsync("Newest", "C", createdAt: now);

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync(null, 20, null, principal, CancellationToken.None);

        result.Songs.Select(s => s.Id).ShouldBe(new[] { featuredId, newestId, oldId });
        result.NextPageToken.ShouldBeNull();
    }

    [Test]
    public async Task List_Query_MatchesTitleOrArtist_CaseInsensitive()
    {
        var (_, principal) = await CreateUserAsync("reader");
        var now = DateTimeOffset.UtcNow;
        var match1 = await SeedSongAsync("Nightmare", "Avenged Sevenfold", createdAt: now.AddMinutes(-3));
        var match2 = await SeedSongAsync("Warmness on the Soul", "Avenged Sevenfold", createdAt: now.AddMinutes(-2));
        await SeedSongAsync("Smells Like Teen Spirit", "Nirvana", createdAt: now.AddMinutes(-4));
        var featuredMatch = await SeedSongAsync("A Little Piece of Heaven", "Avenged Sevenfold", featured: true, createdAt: now.AddMinutes(-1));

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync("avenged", 20, null, principal, CancellationToken.None);

        result.Songs.Select(s => s.Id).ShouldBe(new[] { featuredMatch, match2, match1 });
        result.Songs.All(s => s.Artist == "Avenged Sevenfold").ShouldBeTrue();
    }

    [Test]
    public async Task List_Query_NoMatches_ReturnsEmpty()
    {
        var (_, principal) = await CreateUserAsync("reader");
        await SeedSongAsync("Nightmare", "Avenged Sevenfold");

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync("zzz-nothing", 20, null, principal, CancellationToken.None);

        result.Songs.ShouldBeEmpty();
    }

    [Test]
    public async Task List_Pagination_ReturnsNextPageTokenAndSecondPage()
    {
        var (_, principal) = await CreateUserAsync("reader");
        var now = DateTimeOffset.UtcNow;
        var oldestId = await SeedSongAsync("Song1", "A", createdAt: now.AddHours(-3));
        var middleId = await SeedSongAsync("Song2", "B", createdAt: now.AddHours(-2));
        var newestId = await SeedSongAsync("Song3", "C", createdAt: now.AddHours(-1));

        using var scope = new SongScope();
        var page1 = await scope.Songs.ListAsync(null, 2, null, principal, CancellationToken.None);
        page1.Songs.Select(s => s.Id).ShouldBe(new[] { newestId, middleId });
        page1.NextPageToken.ShouldBe("2");

        var page2 = await scope.Songs.ListAsync(null, 2, page1.NextPageToken, principal, CancellationToken.None);
        page2.Songs.Select(s => s.Id).ShouldBe(new[] { oldestId });
        page2.NextPageToken.ShouldBeNull();
    }

    [Test]
    public async Task List_InvalidPageSize_FallsBackToDefault()
    {
        var (_, principal) = await CreateUserAsync("reader");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await SeedSongAsync($"Song{i}", "A", createdAt: now.AddMinutes(-i));
        }

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync(null, 0, null, principal, CancellationToken.None);
        result.Songs.Count.ShouldBe(5);
        result.NextPageToken.ShouldBeNull();

        result = await scope.Songs.ListAsync(null, 500, null, principal, CancellationToken.None);
        result.Songs.Count.ShouldBe(5);
    }

    [Test]
    public async Task List_InvalidPageToken_FallsBackToZero()
    {
        var (_, principal) = await CreateUserAsync("reader");
        var now = DateTimeOffset.UtcNow;
        var newestId = await SeedSongAsync("Song1", "A", createdAt: now);

        using var scope = new SongScope();
        var result = await scope.Songs.ListAsync(null, 1, "not-a-number", principal, CancellationToken.None);
        result.Songs.Select(s => s.Id).ShouldBe(new[] { newestId });
    }

    [Test]
    public async Task List_AssignmentCount_CountsDistinctRoles()
    {
        var (owner, ownerPrincipal) = await CreateUserAsync("owner", editOwnSongs: true, editOwnParticipation: true);
        var (member, memberPrincipal) = await CreateUserAsync("member", editOwnParticipation: true);
        var songId = await SeedSongAsync(roles: new[] { "Вокал", "Барабаны" }, createdById: owner.Id);

        using (var scope = new SongScope())
        {
            var vocalRoleId = await FindRoleIdAsync(songId, "Вокал");
            await scope.Songs.JoinRoleAsync(owner, ownerPrincipal, vocalRoleId, CancellationToken.None);
        }

        using (var scope = new SongScope())
        {
            var drumsRoleId = await FindRoleIdAsync(songId, "Барабаны");
            await scope.Songs.JoinRoleAsync(member, memberPrincipal, drumsRoleId, CancellationToken.None);
        }

        using (var scope = new SongScope())
        {
            var result = await scope.Songs.ListAsync(null, 20, null, ownerPrincipal, CancellationToken.None);
            result.Songs.Single().Roles.Count(r => r.Assignment != null).ShouldBe(2);
        }
    }
}
