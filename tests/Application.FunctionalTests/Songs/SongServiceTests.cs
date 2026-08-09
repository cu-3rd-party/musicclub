using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using SongEntity = CuMusicClub.Domain.Entities.Song;
using CuMusicClub.Application.Song;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Application.FunctionalTests.Songs;

public partial class SongServiceTests : TestBase
{
    private const string YoutubeUrl = "https://www.youtube.com/watch?v=fJ9rUzIMcZQ";

    private sealed class SongScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public ISongService Songs { get; }

        public SongScope()
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            Songs = _scope.ServiceProvider.GetRequiredService<ISongService>();
        }

        public void Dispose() => _scope.Dispose();
    }

    private static ApplicationDbContext Db()
    {
        var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private static async Task<ClaimsPrincipal> CreateUserAsync(
        string username,
        bool editOwnParticipation = false,
        bool editAnyParticipation = false,
        bool editOwnSongs = false,
        bool editAnySongs = false,
        bool editFeaturedSongs = false)
    {
        var userId = Guid.NewGuid();
        await TestApp.AddAsync(new ApplicationUser
        {
            Id = userId,
            UserName = username,
            DisplayName = $"Display {username}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (editOwnParticipation)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, Permissions.ParticipationEditOwn));
        }
        if (editAnyParticipation)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, Permissions.ParticipationEditAny));
        }
        if (editOwnSongs)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, Permissions.SongsEditOwn));
        }
        if (editAnySongs)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, Permissions.SongsEditAny));
        }
        if (editFeaturedSongs)
        {
            claims.Add(new Claim(PermissionClaimTypes.Permission, Permissions.SongsEditFeatured));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<Guid> SeedSongAsync(
        string title = "Bohemian Rhapsody",
        string artist = "Queen",
        string[]? roles = null,
        bool featured = false,
        Guid? createdById = null,
        DateTimeOffset? createdAt = null)
    {
        var songId = Guid.NewGuid();
        var now = createdAt ?? DateTimeOffset.UtcNow;
        await TestApp.AddAsync(new SongEntity
        {
            Id = songId,
            Title = title,
            Artist = artist,
            Description = null,
            LinkKind = SongLinkType.Youtube,
            LinkUrl = YoutubeUrl,
            CreatedById = createdById,
            ThumbnailUrl = null,
            IsFeatured = featured,
            CreatedAt = now,
            UpdatedAt = now,
        });

        if (roles is not null)
        {
            foreach (var role in roles)
            {
                await TestApp.AddAsync(new SongRole { SongId = songId, Role = role });
            }
        }

        return songId;
    }

    private static CreateSongRequest CreateRequest(
        string title = "Bohemian Rhapsody",
        string artist = "Queen",
        string? url = null,
        bool featured = false,
        string? thumbnailUrl = null,
        string? description = null,
        string[]? roles = null)
        => new(title, artist, description, url ?? YoutubeUrl, thumbnailUrl, featured, roles);
}
