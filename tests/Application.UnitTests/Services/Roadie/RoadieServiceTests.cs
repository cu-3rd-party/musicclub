using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Constants;
using CuMusicClub.Domain.Entities;
using Ardalis.GuardClauses;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Roadie;

[TestFixture]
[TestOf(typeof(RoadieService))]
public class RoadieServiceTests
{
    protected Mock<ISongRepository> _songs = null!;
    protected Mock<IRoadieTicketRepository> _tickets = null!;
    protected Mock<ISongRoadieRepository> _roadies = null!;
    protected Mock<IApplicationUserRepository> _users = null!;
    protected Mock<IPermissionService> _permissions = null!;
    protected Mock<ITelegramChatService> _telegram = null!;
    protected Mock<ISongTopicRepository> _songTopics = null!;
    protected RoadieService _service = null!;

    protected readonly Guid _userId = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _songs = new Mock<ISongRepository>();
        _tickets = new Mock<IRoadieTicketRepository>();
        _roadies = new Mock<ISongRoadieRepository>();
        _users = new Mock<IApplicationUserRepository>();
        _permissions = new Mock<IPermissionService>();
        _telegram = new Mock<ITelegramChatService>();
        _songTopics = new Mock<ISongTopicRepository>();

        _service = new RoadieService(_songs.Object,
            _tickets.Object,
            _roadies.Object,
            _users.Object,
            _permissions.Object,
            _telegram.Object,
            _songTopics.Object);
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

    private static CuMusicClub.Domain.Entities.Song BuildSong(Guid? id = null,
        Guid? createdById = null,
        IEnumerable<SongRoleAssignment>? assignments = null)
    {
        var song = new CuMusicClub.Domain.Entities.Song
        {
            Id = id ?? Guid.NewGuid(),
            Title = "Song",
            Artist = "Artist",
            CreatedById = createdById,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        if (assignments is not null)
        {
            foreach (var a in assignments)
            {
                var role = new SongRole
                {
                    Id = a.RoleId,
                    SongId = song.Id,
                    Song = song,
                    RoleTitle = $"Role_{a.RoleId}",
                    Assignment = a,
                };
                song.Roles.Add(role);
            }
        }
        return song;
    }

    private static SongRoleAssignment Assignment(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        SongId = Guid.NewGuid(),
        RoleId = Guid.NewGuid(),
        UserId = userId,
        JoinedAt = DateTimeOffset.UtcNow,
    };

    private static RoadieTicket OpenTicket(Guid songId) => new()
    {
        Id = Guid.NewGuid(),
        SongId = songId,
        CreatedById = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [TestFixture]
    public class CreateTicketAsyncTests : RoadieServiceTests
    {
        [Test]
        public async Task Participant_CreatesTicket_AndNotifiesRoadieChatAndTopic()
        {
            var user = CurrentUser();
            var song = BuildSong(assignments: new[] { Assignment(_userId) });
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _tickets
                .Setup(r => r.HasOpenTicketAsync(song.Id, It.IsAny<RoadieTicketType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var topic = new SongTopic { Song = song, SongId = song.Id, TopicId = 111 };
            _songTopics
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _telegram
                .Setup(t => t.SendRoadieTicketNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<SongRole>>(), It.IsAny<RoadieTicketType>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            RoadieTicket? added = null;
            _tickets
                .Setup(r => r.AddAsync(It.IsAny<RoadieTicket>(), It.IsAny<CancellationToken>()))
                .Callback<RoadieTicket, CancellationToken>((t, _) => added = t)
                .Returns(Task.CompletedTask);

            var result = await _service.CreateTicketAsync(song.Id, user, RoadieTicketType.Help, CancellationToken.None);

            added.ShouldNotBeNull();
            added!.SongId.ShouldBe(song.Id);
            added.CreatedById.ShouldBe(_userId);
            result.Id.ShouldBe(added.Id);
            result.IsOpen.ShouldBeTrue();
            _telegram.Verify(t => t.SendRoadieTicketNotification(added.Id, song.Title, song.Artist, It.IsAny<IEnumerable<SongRole>>(), It.IsAny<RoadieTicketType>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _telegram.Verify(t => t.SendTopicMessage(111, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task NotParticipant_ThrowsForbidden()
        {
            var user = CurrentUser();
            var song = BuildSong(createdById: Guid.NewGuid());
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.CreateTicketAsync(song.Id, user, RoadieTicketType.Help, CancellationToken.None));

            _tickets.Verify(r => r.AddAsync(It.IsAny<RoadieTicket>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task OpenTicketExists_ReturnsExisting_WithoutDuplicateOrNotification()
        {
            var user = CurrentUser();
            var song = BuildSong(assignments: new[] { Assignment(_userId) });
            _songs
                .Setup(r => r.FindByIdWithDetailsAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            var existing = OpenTicket(song.Id);
            existing.RoadieTicketType = RoadieTicketType.Help;
            _tickets
                .Setup(r => r.HasOpenTicketAsync(song.Id, It.IsAny<RoadieTicketType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _tickets
                .Setup(r => r.Query())
                .Returns(new[] { existing, }.AsQueryable());

            var result = await _service.CreateTicketAsync(song.Id, user, RoadieTicketType.Help, CancellationToken.None);

            result.Id.ShouldBe(existing.Id);
            result.IsOpen.ShouldBeTrue();
            _tickets.Verify(r => r.AddAsync(It.IsAny<RoadieTicket>(), It.IsAny<CancellationToken>()), Times.Never);
            _telegram.Verify(t => t.SendRoadieTicketNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<SongRole>>(), It.IsAny<RoadieTicketType>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _telegram.Verify(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [TestFixture]
    public class AcceptTicketAsyncTests : RoadieServiceTests
    {
        [Test]
        public async Task RoadieWithPermission_Accepts_AndCreatesSongRoadie()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.RoadieManage);
            user.TgUserId = 1001;
            _users
                .Setup(r => r.FindByTgUserIdAsync(1001, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            var song = BuildSong();
            var ticket = OpenTicket(song.Id);
            _tickets
                .Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ticket);
            var topic = new SongTopic { Song = song, SongId = song.Id, TopicId = 222 };
            _songTopics
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            SongRoadie? roadie = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((s, _) => roadie = s)
                .Returns(Task.CompletedTask);

            var result = await _service.AcceptTicketAsync(1001, ticket.Id, CancellationToken.None);

            result.ShouldBe(RoadieAcceptResult.Accepted);
            ticket.AcceptedById.ShouldBe(user.Id);
            roadie.ShouldNotBeNull();
            roadie!.SongId.ShouldBe(ticket.SongId);
            roadie.RoadieId.ShouldBe(user.Id);
            _telegram.Verify(t => t.SendTopicMessage(222, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task UserWithoutPermission_ReturnsNotARoadie()
        {
            var user = CurrentUser();
            user.TgUserId = 1001;
            _users
                .Setup(r => r.FindByTgUserIdAsync(1001, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var result = await _service.AcceptTicketAsync(1001, Guid.NewGuid(), CancellationToken.None);

            result.ShouldBe(RoadieAcceptResult.NotARoadie);
            _tickets.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AlreadyAccepted_ReturnsAlreadyAccepted()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.RoadieManage);
            user.TgUserId = 1001;
            _users
                .Setup(r => r.FindByTgUserIdAsync(1001, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            var song = BuildSong();
            var ticket = OpenTicket(song.Id);
            ticket.AcceptedById = Guid.NewGuid();
            ticket.AcceptedAt = DateTimeOffset.UtcNow;
            _tickets
                .Setup(r => r.GetByIdWithDetailsAsync(ticket.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ticket);

            var result = await _service.AcceptTicketAsync(1001, ticket.Id, CancellationToken.None);

            result.ShouldBe(RoadieAcceptResult.AlreadyAccepted);
            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task MissingTicket_ReturnsNotFound()
        {
            var user = CurrentUser(CuMusicClub.Domain.Constants.Permission.RoadieManage);
            user.TgUserId = 1001;
            _users
                .Setup(r => r.FindByTgUserIdAsync(1001, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
            var ticketId = Guid.NewGuid();
            _tickets
                .Setup(r => r.GetByIdWithDetailsAsync(ticketId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((RoadieTicket?)null);

            var result = await _service.AcceptTicketAsync(1001, ticketId, CancellationToken.None);

            result.ShouldBe(RoadieAcceptResult.NotFound);
        }
    }

    [TestFixture]
    public class AutoAssignOpenTicketsAsyncTests : RoadieServiceTests
    {
        [Test]
        public async Task AssignsMinimallyLoadedRoadie_PreferringThoseWithTgUserId()
        {
            var heavy = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Heavy", TgUserId = 1001 };
            var light = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Light", TgUserId = 1002 };
            var noTg = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "NoTg", TgUserId = null };

            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieAutoAssign, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { heavy, light, noTg, });

            var song = BuildSong();
            var ticket = OpenTicket(song.Id);
            ticket.Song = song;
            _tickets
                .Setup(r => r.GetOpenTicketsOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { ticket, });

            var roadieList = new List<SongRoadie>
            {
                new() { SongId = Guid.NewGuid(), RoadieId = heavy.Id, AssignedAt = DateTimeOffset.UtcNow },
                new() { SongId = Guid.NewGuid(), RoadieId = heavy.Id, AssignedAt = DateTimeOffset.UtcNow },
            };
            _roadies
                .Setup(r => r.Query())
                .Returns(roadieList.AsQueryable());

            var topic = new SongTopic { Song = song, SongId = song.Id, TopicId = 333 };
            _songTopics
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);
            _telegram
                .Setup(t => t.SendDirectMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            SongRoadie? assigned = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((s, _) => assigned = s)
                .Returns(Task.CompletedTask);

            var count = await _service.AutoAssignOpenTicketsAsync(CancellationToken.None);

            count.ShouldBe(1);
            ticket.AcceptedById.ShouldBe(light.Id);
            assigned.ShouldNotBeNull();
            assigned!.RoadieId.ShouldBe(light.Id);
            _telegram.Verify(t => t.SendDirectMessage(light.TgUserId!.Value, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _telegram.Verify(t => t.SendTopicMessage(333, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task NoOverdueTickets_ReturnsZero()
        {
            _tickets
                .Setup(r => r.GetOpenTicketsOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RoadieTicket>());

            var count = await _service.AutoAssignOpenTicketsAsync(CancellationToken.None);

            count.ShouldBe(0);
        }

        [Test]
        public async Task NoRoadieUsers_ReturnsZero()
        {
            var song = BuildSong();
            var ticket = OpenTicket(song.Id);
            _tickets
                .Setup(r => r.GetOpenTicketsOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { ticket, });
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieAutoAssign, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ApplicationUser>());

            var count = await _service.AutoAssignOpenTicketsAsync(CancellationToken.None);

            count.ShouldBe(0);
            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
            _telegram.Verify(t => t.SendRoadieMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AllRoadiesLackTgUserId_UsesRoadieMessageFallback()
        {
            var noTgA = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "A", TgUserId = null };
            var noTgB = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "B", TgUserId = null };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieAutoAssign, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { noTgA, noTgB, });

            var song = BuildSong();
            var ticket = OpenTicket(song.Id);
            ticket.Song = song;
            _tickets
                .Setup(r => r.GetOpenTicketsOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { ticket, });
            _roadies
                .Setup(r => r.Query())
                .Returns(new List<SongRoadie>().AsQueryable());

            var topic = new SongTopic { Song = song, SongId = song.Id, TopicId = 444 };
            _songTopics
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);
            _telegram
                .Setup(t => t.SendRoadieMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            SongRoadie? assigned = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((s, _) => assigned = s)
                .Returns(Task.CompletedTask);

            var count = await _service.AutoAssignOpenTicketsAsync(CancellationToken.None);

            count.ShouldBe(1);
            assigned.ShouldNotBeNull();
            (assigned!.RoadieId == noTgA.Id || assigned.RoadieId == noTgB.Id).ShouldBeTrue();
            _telegram.Verify(t => t.SendRoadieMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _telegram.Verify(t => t.SendTopicMessage(444, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task MultipleOpenTicketsForSameSong_AssignsOnlyOneRoadieAndNotifiesOnce()
        {
            var roadie = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Roadie", TgUserId = 2001 };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieAutoAssign, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { roadie, });

            var song = BuildSong();
            var ticket1 = OpenTicket(song.Id);
            var ticket2 = OpenTicket(song.Id);
            _tickets
                .Setup(r => r.GetOpenTicketsOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { ticket1, ticket2, });
            _roadies
                .Setup(r => r.Query())
                .Returns(new List<SongRoadie>().AsQueryable());

            var topic = new SongTopic { Song = song, SongId = song.Id, TopicId = 555 };
            _songTopics
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topic);
            _telegram
                .Setup(t => t.SendDirectMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _telegram
                .Setup(t => t.SendTopicMessage(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            SongRoadie? assigned = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((s, _) => assigned = s)
                .Returns(Task.CompletedTask);

            var count = await _service.AutoAssignOpenTicketsAsync(CancellationToken.None);

            count.ShouldBe(1);
            ticket1.AcceptedById.ShouldBe(roadie.Id);
            ticket2.AcceptedById.ShouldBeNull();
            assigned.ShouldNotBeNull();
            assigned!.SongId.ShouldBe(song.Id);
            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Once);
            _telegram.Verify(t => t.SendDirectMessage(2001, It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [TestFixture]
    public class RoadieManagementTests : RoadieServiceTests
    {
        private ApplicationUser UserWithPermissions(Guid id, string name, params string[] permissions)
        {
            var user = new ApplicationUser { Id = id, UserName = name, DisplayName = name };
            _permissions
                .Setup(p => p.GetPermissionValuesAsync(user, It.IsAny<CancellationToken>()))
                .ReturnsAsync(permissions);
            return user;
        }

        [Test]
        public async Task Assign_NonRoadieActor_ThrowsForbidden()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor");
            var target = UserWithPermissions(Guid.NewGuid(), "Target", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var song = BuildSong();

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.AssignRoadieAsync(song.Id, target, actor, CancellationToken.None));

            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
            _roadies.Verify(r => r.Remove(It.IsAny<SongRoadie>()), Times.Never);
        }

        [Test]
        public async Task Assign_TargetNotRoadie_ThrowsForbidden()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var target = UserWithPermissions(Guid.NewGuid(), "Target");
            var song = BuildSong();
            _songs
                .Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.AssignRoadieAsync(song.Id, target, actor, CancellationToken.None));

            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Assign_SongNotFound_ThrowsNotFound()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var target = UserWithPermissions(Guid.NewGuid(), "Target", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            _songs
                .Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CuMusicClub.Domain.Entities.Song?)null);

            await Should.ThrowAsync<NotFoundException>(() =>
                _service.AssignRoadieAsync(Guid.NewGuid(), target, actor, CancellationToken.None));

            _roadies.Verify(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Assign_NoExistingRoadie_AddsSongRoadie()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var target = UserWithPermissions(Guid.NewGuid(), "Target", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var song = BuildSong();
            _songs
                .Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _roadies
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SongRoadie?)null);

            SongRoadie? added = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((sr, _) => added = sr)
                .Returns(Task.CompletedTask);

            await _service.AssignRoadieAsync(song.Id, target, actor, CancellationToken.None);

            added.ShouldNotBeNull();
            added!.SongId.ShouldBe(song.Id);
            added.RoadieId.ShouldBe(target.Id);
            _roadies.Verify(r => r.Remove(It.IsAny<SongRoadie>()), Times.Never);
            _roadies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Assign_ExistingRoadie_ReplacesIt()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var target = UserWithPermissions(Guid.NewGuid(), "Target", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var song = BuildSong();
            var existing = new SongRoadie
            {
                SongId = song.Id,
                RoadieId = Guid.NewGuid(),
                AssignedAt = DateTimeOffset.UtcNow,
            };
            _songs
                .Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _roadies
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            SongRoadie? added = null;
            _roadies
                .Setup(r => r.AddAsync(It.IsAny<SongRoadie>(), It.IsAny<CancellationToken>()))
                .Callback<SongRoadie, CancellationToken>((sr, _) => added = sr)
                .Returns(Task.CompletedTask);

            await _service.AssignRoadieAsync(song.Id, target, actor, CancellationToken.None);

            added.ShouldNotBeNull();
            added!.RoadieId.ShouldBe(target.Id);
            _roadies.Verify(r => r.Remove(existing), Times.Once);
            _roadies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Remove_NonRoadieActor_ThrowsForbidden()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor");

            await Should.ThrowAsync<ForbiddenAccessException>(() =>
                _service.RemoveRoadieAsync(Guid.NewGuid(), actor, CancellationToken.None));

            _roadies.Verify(r => r.Remove(It.IsAny<SongRoadie>()), Times.Never);
            _roadies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Remove_SongNotFound_ThrowsNotFound()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            _songs
                .Setup(r => r.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CuMusicClub.Domain.Entities.Song?)null);

            await Should.ThrowAsync<NotFoundException>(() =>
                _service.RemoveRoadieAsync(Guid.NewGuid(), actor, CancellationToken.None));

            _roadies.Verify(r => r.Remove(It.IsAny<SongRoadie>()), Times.Never);
        }

        [Test]
        public async Task Remove_NoRoadieAssigned_IsNoOp()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var song = BuildSong();
            _songs
                .Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _roadies
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SongRoadie?)null);

            await _service.RemoveRoadieAsync(song.Id, actor, CancellationToken.None);

            _roadies.Verify(r => r.Remove(It.IsAny<SongRoadie>()), Times.Never);
            _roadies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Remove_RoadieAssigned_RemovesIt()
        {
            var actor = UserWithPermissions(Guid.NewGuid(), "Actor", CuMusicClub.Domain.Constants.Permission.RoadieManage);
            var song = BuildSong();
            var existing = new SongRoadie
            {
                SongId = song.Id,
                RoadieId = Guid.NewGuid(),
                AssignedAt = DateTimeOffset.UtcNow,
            };
            _songs
                .Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(song);
            _roadies
                .Setup(r => r.FindBySongIdAsync(song.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            await _service.RemoveRoadieAsync(song.Id, actor, CancellationToken.None);

            _roadies.Verify(r => r.Remove(existing), Times.Once);
            _roadies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Candidates_NoQuery_ReturnsAllSortedByDisplayName()
        {
            var bravo = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Bravo" };
            var alpha = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "alpha" };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { bravo, alpha });

            var result = (await _service.GetRoadieCandidatesAsync(null, CancellationToken.None)).ToList();

            result.Count.ShouldBe(2);
            result[0].DisplayName.ShouldBe("alpha");
            result[1].DisplayName.ShouldBe("Bravo");
        }

        [Test]
        public async Task Candidates_Query_FiltersByDisplayNameCaseInsensitive()
        {
            var alice = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Alice" };
            var bob = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Bob" };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { alice, bob });

            var result = (await _service.GetRoadieCandidatesAsync("ALI", CancellationToken.None)).ToList();

            result.Count.ShouldBe(1);
            result[0].DisplayName.ShouldBe("Alice");
        }

        [Test]
        public async Task Candidates_Query_FiltersByUsername()
        {
            var alice = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Alice", UserName = "alice_roadie" };
            var bob = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Bob" };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { alice, bob });

            var result = (await _service.GetRoadieCandidatesAsync("roadie", CancellationToken.None)).ToList();

            result.Count.ShouldBe(1);
            result[0].DisplayName.ShouldBe("Alice");
        }

        [Test]
        public async Task Candidates_NoMatch_ReturnsEmpty()
        {
            var alice = new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Alice" };
            _users
                .Setup(r => r.GetUsersByPermissionAsync(CuMusicClub.Domain.Constants.Permission.RoadieManage, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { alice });

            var result = (await _service.GetRoadieCandidatesAsync("zzz", CancellationToken.None)).ToList();

            result.Count.ShouldBe(0);
        }
    }
}
