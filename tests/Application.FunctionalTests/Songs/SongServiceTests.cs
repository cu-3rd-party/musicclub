using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Songs;
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

    private static async Task<Guid> CreateUserAsync(
        string username,
        bool editOwnParticipation = false,
        bool editAnyParticipation = false,
        bool editOwnSongs = false,
        bool editAnySongs = false,
        bool editFeaturedSongs = false)
    {
        var userId = Guid.NewGuid();
        await TestApp.AddAsync(new AppUser
        {
            Id = userId,
            Username = username,
            DisplayName = $"Display {username}",
        });
        await TestApp.AddAsync(new UserPermission
        {
            UserId = userId,
            EditOwnParticipation = editOwnParticipation,
            EditAnyParticipation = editAnyParticipation,
            EditOwnSongs = editOwnSongs,
            EditAnySongs = editAnySongs,
            EditFeaturedSongs = editFeaturedSongs,
        });
        return userId;
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
        await TestApp.AddAsync(new Song
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
        string kind = "youtube",
        string? url = YoutubeUrl,
        bool featured = false,
        string? thumbnailUrl = null,
        string? description = null,
        string[]? roles = null)
        => new(title, artist, description, new SongLinkDto(kind, url), thumbnailUrl, featured, roles);
}
