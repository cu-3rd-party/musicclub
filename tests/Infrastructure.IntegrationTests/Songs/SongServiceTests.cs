using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SongEntity = CuMusicClub.Domain.Entities.Song;
using SongRoleAssignmentEntity = CuMusicClub.Domain.Entities.SongRoleAssignment;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

public partial class SongServiceTests : TestBase
{
    private const string YoutubeUrl = "https://www.youtube.com/watch?v=fJ9rUzIMcZQ";

    private sealed class SongScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public ISongService Songs { get; }
        public IDataEntryService DataEntryService { get; }
        public IApplicationUserRepository Users { get; }

        public SongScope()
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            Songs = _scope.ServiceProvider.GetRequiredService<ISongService>();
            DataEntryService = _scope.ServiceProvider.GetRequiredService<IDataEntryService>();
            Users = _scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }

    private static ApplicationDbContext Db()
    {
        var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private static async Task<(ApplicationUser AppUser, ClaimsPrincipal Principal)> CreateUserAsync(string username,
        bool editOwnParticipation = false,
        bool editAnyParticipation = false,
        bool editOwnSongs = false,
        bool editAnySongs = false,
        bool editFeaturedSongs = false)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();

        var user = new ApplicationUser
        {
            UserName = username,
            DisplayName = $"Display {username}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(user);

        var permissions = new List<string>();
        if (editOwnParticipation) permissions.Add(Permission.ParticipationEditOwn);
        if (editAnyParticipation) permissions.Add(Permission.ParticipationEditAny);
        if (editOwnSongs) permissions.Add(Permission.SongsEditOwn);
        if (editAnySongs) permissions.Add(Permission.SongsEditAny);
        if (editFeaturedSongs) permissions.Add(Permission.SongsEditFeatured);

        await users.GrantPermissionsAsync(user.Id, permissions);
        await users.SaveChangesAsync();

        var identity = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            ],
            "test");

        return (user, new ClaimsPrincipal(identity));
    }

    private static async Task<Guid> SeedSongAsync(string title = "Bohemian Rhapsody",
        string artist = "Queen",
        string[]? roles = null,
        bool featured = false,
        Guid? createdById = null,
        DateTimeOffset? createdAt = null)
    {
        var songId = Guid.NewGuid();
        var now = createdAt ?? DateTimeOffset.UtcNow;

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var song = new SongEntity
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
        };
        db.Songs.Add(song);
        await db.SaveChangesAsync();

        if (roles is not null)
        {
            foreach (var role in roles)
                db.SongRoles.Add(new SongRole
                {
                    SongId = songId,
                    Song = song,
                    RoleTitle = role,
                });

            await db.SaveChangesAsync();
        }

        return songId;
    }

    private static async Task<Guid> FindRoleIdAsync(Guid songId, string roleTitle)
    {
        using var db = Db();
        var role = await db.SongRoles.FirstOrDefaultAsync(r => r.SongId == songId && r.RoleTitle == roleTitle);
        return role?.Id ?? throw new InvalidOperationException($"Role '{roleTitle}' not found on song {songId}");
    }

    private static async Task SeedAssignmentAsync(Guid songId, string roleTitle, Guid userId)
    {
        var roleId = await FindRoleIdAsync(songId, roleTitle);

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.FirstAsync(u => u.Id == userId);

        db.SongRoleAssignments.Add(new SongRoleAssignmentEntity
        {
            SongId = songId,
            RoleId = roleId,
            UserId = userId,
            User = user,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static CreateSongRequest CreateRequest(string title = "Bohemian Rhapsody",
        string artist = "Queen",
        string? url = null,
        bool featured = false,
        Guid? thumbnailDataEntryId = null,
        string? description = null,
        string[]? roles = null)
    {
        return new CreateSongRequest(title, artist, description, url ?? YoutubeUrl, thumbnailDataEntryId, featured, roles);
    }
}
