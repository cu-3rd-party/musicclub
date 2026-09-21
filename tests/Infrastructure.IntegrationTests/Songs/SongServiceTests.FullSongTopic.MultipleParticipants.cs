using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CuMusicClub.Infrastructure.IntegrationTests.Songs;

/// <summary>
/// Тесты проверяют, что при создании топика для заполненной песни
/// сообщение содержит упоминания всех участников, а не только первого.
/// </summary>
public class SongServiceFullSongTopicMultipleParticipantsTests : TestBase
{
    private Mock<ITelegramChatService> _telegramMock = null!;

    [SetUp]
    public void SetUpTelegramMock()
    {
        _telegramMock = new Mock<ITelegramChatService>();
        _telegramMock
            .Setup(t => t.CreateTopic(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string title, Guid songId, CancellationToken ct) =>
            {
                var song = new Song
                {
                    Id = songId,
                    Title = title.Split(" — ").Length > 0 ? title.Split(" — ")[0] : title,
                    Artist = title.Split(" — ").Length > 1 ? title.Split(" — ")[1] : "",
                };
                return new SongTopic { Song = song, SongId = songId, TopicId = 999, Title = title };
            });
        _telegramMock
            .Setup(t => t.SendGeneralMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _telegramMock
            .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _telegramMock
            .Setup(t => t.BuildSongSystemLink(It.IsAny<Song>()))
            .Returns("https://localhost/song/123");
    }

    [Test]
    public async Task FullSongWithMultipleParticipants_CreatesTopicWithAllMentions()
    {
        // Arrange: создаём пользователей с разными Telegram ID
        var (vocalUser, vocalPrincipal) = await CreateUserAsync("vocalist", editOwnParticipation: true, tgUserId: 111);
        var (guitarUser, guitarPrincipal) = await CreateUserAsync("guitarist", editOwnParticipation: true, tgUserId: 222);
        var (drumsUser, drumsPrincipal) = await CreateUserAsync("drummer", editOwnParticipation: true, tgUserId: 333);

        var songId = await SeedSongAsync(
            title: "Bohemian Rhapsody",
            artist: "Queen",
            roles: new[] { "Вокал", "Гитара", "Барабаны" });

        var vocalRoleId = await FindRoleIdAsync(songId, "Вокал");
        var guitarRoleId = await FindRoleIdAsync(songId, "Гитара");
        var drumsRoleId = await FindRoleIdAsync(songId, "Барабаны");

        // Act: каждый пользователь присоединяется к своей роли
        SongDto result;
        using (var scope = new SongScope(_telegramMock.Object))
        {
            // Присоединяем первого участника
            await scope.Songs.JoinRoleAsync(vocalUser, vocalPrincipal, vocalRoleId, CancellationToken.None);
            // Присоединяем второго участника
            await scope.Songs.JoinRoleAsync(guitarUser, guitarPrincipal, guitarRoleId, CancellationToken.None);
            // Присоединяем третьего участника — песня становится полной, создаётся топик
            result = await scope.Songs.JoinRoleAsync(drumsUser, drumsPrincipal, drumsRoleId, CancellationToken.None);
        }

        // Assert: проверяем, что все участники назначены
        result.Roles.Count(r => r.Assignment != null).ShouldBe(3);

        // Проверяем, что сообщение в топик содержит упоминания всех трёх участников
        _telegramMock.Verify(
            t => t.SendTopicMessage(
                999,
                It.Is<string>(msg =>
                    msg.Contains("tg://user?id=111") &&
                    msg.Contains("tg://user?id=222") &&
                    msg.Contains("tg://user?id=333") &&
                    msg.Contains("Display vocalist") &&
                    msg.Contains("Display guitarist") &&
                    msg.Contains("Display drummer")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task FullSongWithMixedTgUserIds_CreatesTopicWithCorrectMentions()
    {
        // Arrange: создаём пользователей, один из которых без Telegram
        var (vocalUser, vocalPrincipal) = await CreateUserAsync("vocalist", editOwnParticipation: true, tgUserId: 111);
        var (guitarUser, guitarPrincipal) = await CreateUserAsync("guitarist", editOwnParticipation: true, tgUserId: null); // без Telegram
        var (drumsUser, drumsPrincipal) = await CreateUserAsync("drummer", editOwnParticipation: true, tgUserId: 333);

        var songId = await SeedSongAsync(
            title: "Song",
            artist: "Artist",
            roles: new[] { "Вокал", "Гитара", "Барабаны" });

        var vocalRoleId = await FindRoleIdAsync(songId, "Вокал");
        var guitarRoleId = await FindRoleIdAsync(songId, "Гитара");
        var drumsRoleId = await FindRoleIdAsync(songId, "Барабаны");

        // Act: присоединяем участников
        using (var scope = new SongScope(_telegramMock.Object))
        {
            await scope.Songs.JoinRoleAsync(vocalUser, vocalPrincipal, vocalRoleId, CancellationToken.None);
            await scope.Songs.JoinRoleAsync(guitarUser, guitarPrincipal, guitarRoleId, CancellationToken.None);
            await scope.Songs.JoinRoleAsync(drumsUser, drumsPrincipal, drumsRoleId, CancellationToken.None);
        }

        // Assert: проверяем сообщение
        // vocal и drums должны быть с tg:// ссылками, guitar — только именем
        _telegramMock.Verify(
            t => t.SendTopicMessage(
                999,
                It.Is<string>(msg =>
                    msg.Contains("tg://user?id=111") && // vocal с Telegram
                    msg.Contains("tg://user?id=333") && // drums с Telegram
                    msg.Contains("Display guitarist") && // guitar без Telegram — только имя
                    !msg.Contains("tg://user?id=" + guitarUser.Id)), // убедимся, что нет ссылки для guitar
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ClaimsPrincipal CreatePrincipal(ApplicationUser user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        }, "test");

        return new ClaimsPrincipal(identity);
    }

    private static async Task<(ApplicationUser AppUser, ClaimsPrincipal Principal)> CreateUserAsync(
        string username,
        bool editOwnParticipation = false,
        long? tgUserId = null)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();

        var user = new ApplicationUser
        {
            UserName = username,
            DisplayName = $"Display {username}",
            TgUserId = tgUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await users.AddAsync(user);

        var permissions = new List<string>();
        if (editOwnParticipation) permissions.Add(Permission.ParticipationEditOwn);

        await users.GrantPermissionsAsync(user.Id, permissions);
        await users.SaveChangesAsync();

        var principal = CreatePrincipal(user);
        return (user, principal);
    }

    private static async Task<Guid> SeedSongAsync(
        string title = "Bohemian Rhapsody",
        string artist = "Queen",
        string[]? roles = null)
    {
        var songId = Guid.NewGuid();
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var song = new Domain.Entities.Song
        {
            Id = songId,
            Title = title,
            Artist = artist,
            Description = null,
            LinkKind = Domain.Enums.SongLinkType.Youtube,
            LinkUrl = "https://www.youtube.com/watch?v=fJ9rUzIMcZQ",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Songs.Add(song);
        await db.SaveChangesAsync();

        if (roles is not null)
        {
            foreach (var role in roles)
                db.SongRoles.Add(new SongRole
                {
                    SongId = songId,
                    RoleTitle = role,
                });

            await db.SaveChangesAsync();
        }

        return songId;
    }

    private static async Task<Guid> FindRoleIdAsync(Guid songId, string roleTitle)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = await db.SongRoles.FirstOrDefaultAsync(r => r.SongId == songId && r.RoleTitle == roleTitle);
        return role?.Id ?? throw new InvalidOperationException($"Role '{roleTitle}' not found on song {songId}");
    }

    private sealed class SongScope : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly SongService _songService;

        public ISongService Songs => _songService;

        public SongScope(ITelegramChatService telegramChatService)
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            
            // Получаем все зависимости из scope и создаём SongService вручную с подменённым Telegram
            var permissionService = _scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var songRepo = _scope.ServiceProvider.GetRequiredService<ISongRepository>();
            var songRoleRepo = _scope.ServiceProvider.GetRequiredService<ISongRoleRepository>();
            var songRoleAssignmentRepo = _scope.ServiceProvider.GetRequiredService<ISongRoleAssignmentRepository>();
            var songTopicRepo = _scope.ServiceProvider.GetRequiredService<ISongTopicRepository>();
            var dataEntryRepo = _scope.ServiceProvider.GetRequiredService<IDataEntryRepository>();
            var unitOfWork = _scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userRepo = _scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
            var roadieService = _scope.ServiceProvider.GetRequiredService<IRoadieService>();
            
            _songService = new SongService(
                permissionService,
                songRepo,
                songRoleRepo,
                songRoleAssignmentRepo,
                songTopicRepo,
                dataEntryRepo,
                unitOfWork,
                userRepo,
                roadieService,
                telegramChatService);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
