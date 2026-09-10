using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Ardalis.GuardClauses;
using CuMusicClub.Application.Services.Roadie;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;
using Shouldly;
using SongEntity = CuMusicClub.Domain.Entities.Song;

namespace CuMusicClub.Application.UnitTests.Services.Song;

[TestFixture]
[TestOf(typeof(SongService))]
public class SongServiceTests
{
    protected Mock<IPermissionService> _permissions = null!;
    protected Mock<ISongRepository> _songs = null!;
    protected Mock<ISongRoleRepository> _songRoles = null!;
    protected Mock<ISongRoleAssignmentRepository> _songRoleAssignments = null!;
    protected Mock<ISongTopicRepository> _songTopics = null!;
    protected Mock<IDataEntryRepository> _dataEntries = null!;
    protected Mock<IUnitOfWork> _unitOfWork = null!;
    protected Mock<ITelegramChatService> _telegram = null!;
    protected Mock<IApplicationUserRepository> _users = null!;
    protected Mock<IRoadieService> _roadie = null!;
    protected SongService _service = null!;

    protected readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _permissions = new Mock<IPermissionService>();
        _songs = new Mock<ISongRepository>();
        _songRoles = new Mock<ISongRoleRepository>();
        _songRoleAssignments = new Mock<ISongRoleAssignmentRepository>();
        _songTopics = new Mock<ISongTopicRepository>();
        _dataEntries = new Mock<IDataEntryRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _telegram = new Mock<ITelegramChatService>();
        _users = new Mock<IApplicationUserRepository>();
        _roadie = new Mock<IRoadieService>();

        _service = new SongService(_permissions.Object,
            _songs.Object,
            _songRoles.Object,
            _songRoleAssignments.Object,
            _songTopics.Object,
            _dataEntries.Object,
            _unitOfWork.Object,
            _users.Object,
            _roadie.Object,
            _telegram.Object);
    }

    protected ClaimsPrincipal Principal()
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, _userId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    protected ApplicationUser CurrentUser(params string[] permissions)
    {
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = "test",
            DisplayName = "Test",
        };
        _users
            .Setup(u => u.FindByIdAsync(_userId))
            .ReturnsAsync(user);
        _permissions
            .Setup(p => p.GetPermissionValuesAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
        return user;
    }

    private static SongEntity BuildSong(Guid? id = null, Guid? createdById = null)
    {
        var song = new SongEntity
        {
            Id = id ?? Guid.NewGuid(),
            Title = "Song",
            Artist = "Artist",
            LinkUrl = "https://youtube.com/watch?v=abc",
            LinkKind = SongLinkType.Youtube,
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        if (createdById.HasValue)
            song.CreatedBy = new ApplicationUser
            {
                Id = createdById.Value,
                UserName = "owner",
                DisplayName = "Owner",
            };
        return song;
    }

    [TestFixture]
    public class ListAsyncTests : SongServiceTests
    {
        [Test]
        public async Task ReturnsSongsMappedToDtos()
        {
            var song = BuildSong();
            _songs
                .Setup(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { song, });

            var result = await _service.ListAsync(null, 20, null, Principal(), CancellationToken.None);

            result.Songs.Count.ShouldBe(1);
            result.Songs[0].Id.ShouldBe(song.Id);
            result.Songs[0].Title.ShouldBe("Song");
        }

        [Test]
        public async Task ClampsPageSizeToDefault_WhenZero()
        {
            var song = BuildSong();
            _songs
                .Setup(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { song, });

            await _service.ListAsync(null, 0, null, Principal(), CancellationToken.None);

            _songs.Verify(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task PageSizeAboveMax_FallsBackToDefault()
        {
            _songs
                .Setup(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SongEntity>());

            await _service.ListAsync(null, 500, null, Principal(), CancellationToken.None);

            _songs.Verify(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task FullPage_SetsNextPageToken()
        {
            var songs = Enumerable.Range(0, 20).Select(_ => BuildSong()).ToArray();
            _songs
                .Setup(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(songs);

            var result = await _service.ListAsync(null, 20, null, Principal(), CancellationToken.None);

            result.NextPageToken.ShouldBe("20");
        }

        [Test]
        public async Task PartialPage_NextPageTokenIsNull()
        {
            var songs = new[]
            {
                BuildSong(),
                BuildSong(),
            };
            _songs
                .Setup(r => r.ListSongsAsync(null, 0, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(songs);

            var result = await _service.ListAsync(null, 20, null, Principal(), CancellationToken.None);

            result.NextPageToken.ShouldBeNull();
        }
    }

    [TestFixture]
    public class GetAsyncTests : SongServiceTests
    {
        [Test]
        public async Task ExistingSong_ReturnsDto()
        {
            var song = BuildSong();
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            var result = await _service.GetAsync(song.Id, CancellationToken.None);

            result.Id.ShouldBe(song.Id);
            result.Title.ShouldBe("Song");
        }

        [Test]
        public async Task MissingSong_ThrowsNotFoundException()
        {
            var id = Guid.NewGuid();
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SongEntity?)null);

            await Should.ThrowAsync<NotFoundException>(() => _service.GetAsync(id, CancellationToken.None));
        }
    }

    [TestFixture]
    public class CreateAsyncTests : SongServiceTests
    {
        private CreateSongRequest Request() => new("Song", "Artist", null, "https://youtube.com/watch?v=abc", null, false, ["Vocal"]);

        [Test]
        public async Task WithoutPermission_ThrowsForbidden()
        {
            CurrentUser();
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ITransaction>());

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.CreateAsync(Request(), Principal(), CancellationToken.None));
        }

        [Test]
        public async Task WithParticipationPermission_CreatesSong()
        {
            CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var transaction = new Mock<ITransaction>();
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            SongEntity? added = null;
            _songs
                .Setup(r => r.AddAsync(It.IsAny<SongEntity>(), It.IsAny<CancellationToken>()))
                .Callback<SongEntity, CancellationToken>((s, _) => added = s)
                .Returns(Task.CompletedTask);
            // GetAsync после создания — вернём сохранённую песню
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => added);
            // ReplaceRolesAsync загружает песню с топиком и ролями
            _songs
                .Setup(r => r.FindByIdWithTopicAndRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var s = added ?? new SongEntity();
                    s.SongTopic = new SongTopic { Song = s, TopicId = 111 };
                    return s;
                });

            var result = await _service.CreateAsync(Request(), Principal(), CancellationToken.None);

            added.ShouldNotBeNull();
            added!.Title.ShouldBe("Song");
            added.CreatedById.ShouldBe(_userId);
            result.Title.ShouldBe("Song");
            transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task FeaturedWithoutPermission_ThrowsForbidden()
        {
            CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ITransaction>());
            var request = Request() with { Featured = true };

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.CreateAsync(request, Principal(), CancellationToken.None));
        }

        [Test]
        public async Task ReferencedDataEntryMissing_ThrowsValidationException()
        {
            CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ITransaction>());
            _dataEntries
                .Setup(r => r.ExistsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var request = Request() with { ThumbnailDataEntryId = Guid.NewGuid() };

            await Should.ThrowAsync<ValidationException>(() =>
                _service.CreateAsync(request, Principal(), CancellationToken.None));
        }
    }

    [TestFixture]
    public class UpdateAsyncTests : SongServiceTests
    {
        private UpdateSongRequest Request() => new("New", "Artist", null, "https://youtube.com/watch?v=abc", null, false, ["Vocal"]);

        [Test]
        public async Task NotOwnerWithoutPermission_ThrowsForbidden()
        {
            CurrentUser();
            var song = BuildSong(createdById: Guid.NewGuid());
            _songs
                .Setup(r => r.FindByIdWithCreatedByAndTopicAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ITransaction>());

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.UpdateAsync(song.Id, Request(), Principal(), CancellationToken.None));
        }

        [Test]
        public async Task Owner_UpdatesSong()
        {
            CurrentUser(CuMusicClub.Domain.Constants.Permission.SongsEditAny);
            var song = BuildSong(createdById: _userId);
            _songs
                .Setup(r => r.FindByIdWithCreatedByAndTopicAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _songRoles
                .Setup(r => r.GetRoleTitlesBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(["Vocal"]);
            var transaction = new Mock<ITransaction>();
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transaction.Object);

            var result = await _service.UpdateAsync(song.Id, Request(), Principal(), CancellationToken.None);

            song.Title.ShouldBe("New");
            result.Title.ShouldBe("New");
            transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [TestFixture]
    public class DeleteAsyncTests : SongServiceTests
    {
        [Test]
        public async Task NotOwnerWithoutPermission_ThrowsForbidden()
        {
            CurrentUser();
            var song = BuildSong(createdById: Guid.NewGuid());
            _songs
                .Setup(r => r.FindByIdWithCreatedByAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.DeleteAsync(song.Id, Principal(), CancellationToken.None));
        }

        [Test]
        public async Task Owner_RemovesSong()
        {
            CurrentUser(CuMusicClub.Domain.Constants.Permission.SongsEditAny);
            var song = BuildSong(createdById: _userId);
            _songs
                .Setup(r => r.FindByIdWithCreatedByAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            await _service.DeleteAsync(song.Id, Principal(), CancellationToken.None);

            _songs.Verify(r => r.Remove(song), Times.Once);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task MissingSong_ThrowsNotFoundException()
        {
            CurrentUser();
            var id = Guid.NewGuid();
            _songs
                .Setup(r => r.FindByIdWithCreatedByAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SongEntity?)null);

            await Should.ThrowAsync<NotFoundException>(() => _service.DeleteAsync(id, Principal(), CancellationToken.None));
        }
    }

    [TestFixture]
    public class JoinRoleAsyncTests : SongServiceTests
    {
        [Test]
        public async Task OccupiedRole_ThrowsBadHttpRequestException()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var song = BuildSong();
            var role = new SongRole
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                Song = song,
                RoleTitle = "Vocal",
                Assignment = new SongRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    SongId = song.Id,
                    RoleId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                },
            };
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            await Should.ThrowAsync<BadHttpRequestException>(() =>
                _service.JoinRoleAsync(user, Principal(), role.Id, CancellationToken.None));
        }

        [Test]
        public async Task MissingRole_ThrowsNotFoundException()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var roleId = Guid.NewGuid();
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(roleId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SongRole?)null);

            await Should.ThrowAsync<NotFoundException>(() =>
                _service.JoinRoleAsync(user, Principal(), roleId, CancellationToken.None));
        }

        [Test]
        public async Task WithoutPermission_ThrowsForbidden()
        {
            var user = CurrentUser();
            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.JoinRoleAsync(user, Principal(), Guid.NewGuid(), CancellationToken.None));
        }

        [Test]
        public async Task FreeRole_AssignsUser()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var song = BuildSong();
            var role = new SongRole
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                Song = song,
                RoleTitle = "Vocal",
            };
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            // после присоединения песня становится полной — мокаем создание топика
            _telegram
                .Setup(t => t.CreateTopic(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SongTopic { Song = song, TopicId = 111 });
            _telegram
                .Setup(t => t.SendGeneralMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _service.JoinRoleAsync(user, Principal(), role.Id, CancellationToken.None);

            role.Assignment.ShouldNotBeNull();
            role.Assignment!.UserId.ShouldBe(_userId);
            _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [TestFixture]
    public class LeaveRoleAsyncTests : SongServiceTests
    {
        [Test]
        public async Task UnoccupiedRole_ThrowsBadHttpRequestException()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var song = BuildSong();
            var role = new SongRole
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                Song = song,
                RoleTitle = "Vocal",
            };
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            await Should.ThrowAsync<BadHttpRequestException>(() =>
                _service.LeaveRoleAsync(user, Principal(), role.Id, CancellationToken.None));
        }

        [Test]
        public async Task OccupiedRole_RemovesAssignment()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditOwn);
            var song = BuildSong();
            var assignment = new SongRoleAssignment
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                RoleId = Guid.NewGuid(),
                UserId = user.Id,
            };
            var role = new SongRole
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                Song = song,
                RoleTitle = "Vocal",
                Assignment = assignment,
            };
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            await _service.LeaveRoleAsync(user, Principal(), role.Id, CancellationToken.None);

            _songRoleAssignments.Verify(r =>
                r.RemoveByIdAsync(assignment.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task SideUserWithEditAny_CanRemoveAnotherUser()
        {
            // Требуется право ParticipationEditAny у того, кто снимает (requester),
            // а не у того, кого снимают (target).
            var requester = CurrentUser(CuMusicClub.Domain.Constants.Permission.ParticipationEditAny);

            var target = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "target",
                DisplayName = "Target",
            };

            var song = BuildSong();
            var assignment = new SongRoleAssignment
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                RoleId = Guid.NewGuid(),
                UserId = target.Id,
            };
            var role = new SongRole
            {
                Id = Guid.NewGuid(),
                SongId = song.Id,
                Song = song,
                RoleTitle = "Vocal",
                Assignment = assignment,
            };
            _songRoles
                .Setup(r => r.FindByIdWithSongAndAssignmentAsync(role.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _users
                .Setup(u => u.GetPreferencesAsync(target.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserPreferences?)null);

            await _service.LeaveRoleAsync(target, Principal(), role.Id, CancellationToken.None);

            _songRoleAssignments.Verify(r =>
                r.RemoveByIdAsync(assignment.Id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
