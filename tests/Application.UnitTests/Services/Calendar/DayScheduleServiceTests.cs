using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Infrastructure.Yandex;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(DayScheduleService))]
public class DayScheduleServiceTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);

    private static DateTimeOffset At(int hour, int minute = 0)
    {
        return new DateTimeOffset(2026, 10, 8, hour, minute, 0, Msk);
    }

    private readonly Guid _songId = Guid.NewGuid();
    private readonly ApplicationUser _withLogin = new() { Id = Guid.NewGuid(), UserName = "xfx1337", DisplayName = "Фёдор", YandexLogin = "f.zhakov" };
    private readonly ApplicationUser _withoutLogin = new() { Id = Guid.NewGuid(), UserName = "nologin", DisplayName = "Без логина" };
    private readonly ApplicationUser _excluded = new() { Id = Guid.NewGuid(), UserName = "skipme", DisplayName = "Пропустить", YandexLogin = "s.kip" };

    private Mock<IRoomScheduleProvider> _room = null!;
    private Mock<IExternalScheduleProvider> _external = null!;
    private Mock<IRehearsalBookingRepository> _bookings = null!;
    private DayScheduleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _room = new Mock<IRoomScheduleProvider>();
        _room.SetupGet(r => r.IsConfigured).Returns(true);
        _room.Setup(r => r.GetRoomEventsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExternalScheduleEvent("Репетиция группы", At(10), At(12)),
                new ExternalScheduleEvent("SLOT", At(14), At(18))
            ]);

        _external = new Mock<IExternalScheduleProvider>();
        _external.Setup(p => p.GetUserEventsAsync("f.zhakov@edu.centraluniversity.ru", It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExternalScheduleEvent("Пара", At(12, 30), At(13, 30))]);

        _bookings = new Mock<IRehearsalBookingRepository>();
        _bookings.Setup(r => r.GetActiveInRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RehearsalBooking { ScheduledAt = At(19), DurationMinutes = 80, Status = BookingStatus.Confirmed }]);

        var assignments = new Mock<ISongRoleAssignmentRepository>();
        assignments.Setup(r => r.GetMemberUserIdsBySongIdAsync(_songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_withLogin.Id, _withoutLogin.Id, _excluded.Id]);

        var users = new Mock<IApplicationUserRepository>();
        foreach (var user in new[] { _withLogin, _withoutLogin, _excluded })
            users.Setup(r => r.FindByIdAsync(It.Is<object[]>(k => (Guid) k[0] == user.Id))).ReturnsAsync(user);

        _service = new DayScheduleService(_room.Object, _external.Object, _bookings.Object, assignments.Object,
            users.Object, NullLogger<DayScheduleService>.Instance);
    }

    [Test]
    public async Task GetDayAsync_BuildsRoomTimelineWithPriorities()
    {
        var day = await _service.GetDayAsync(Day, null, []);

        day.RoomKnown.ShouldBeTrue();
        day.Room.Select(r => (r.Start, r.End, r.Status)).ShouldBe([
            (At(10), At(12), RoomSlotStatus.Busy),
            (At(12), At(14), RoomSlotStatus.Free),
            (At(14), At(18), RoomSlotStatus.Coach),
            (At(18), At(19), RoomSlotStatus.Free),
            (At(19), At(20, 20), RoomSlotStatus.Busy),
            (At(20, 20), At(22), RoomSlotStatus.Free)
        ]);
        day.Members.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDayAsync_MatchesMembersAndFindsCommonWindows()
    {
        var day = await _service.GetDayAsync(Day, _songId, ["@skipme"]);

        day.Members.Count.ShouldBe(2);
        day.Members.ShouldContain(m => m.UserName == "nologin" && m.Status == MemberScheduleStatus.NoYandexLogin);
        day.Members.ShouldContain(m => m.UserName == "xfx1337" && m.Status == MemberScheduleStatus.Ok && m.Events.Count == 1);
        _external.Verify(p => p.GetUserEventsAsync("s.kip@edu.centraluniversity.ru", It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);

        // 12:00–14:00 разрезано парой 12:30–13:30 на куски короче часа
        day.FreeWindows.ShouldBe([
            new FreeWindow(At(14), At(18), true),
            new FreeWindow(At(18), At(19), false),
            new FreeWindow(At(20, 20), At(22), false)
        ]);
    }

    [Test]
    public async Task GetDayAsync_RoomFailure_StillReturnsMembers()
    {
        _room.Setup(r => r.GetRoomEventsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var day = await _service.GetDayAsync(Day, _songId, []);

        day.RoomKnown.ShouldBeFalse();
        day.Members.Count.ShouldBe(3);
    }

    [Test]
    public void ParseIcs_ExpandsRecurrenceAndSkipsOtherDays()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:test
            BEGIN:VEVENT
            UID:weekly
            DTSTART:20261001T120000Z
            DTEND:20261001T133000Z
            RRULE:FREQ=WEEKLY
            SUMMARY:Репетиция
            END:VEVENT
            BEGIN:VEVENT
            UID:other-day
            DTSTART:20261009T120000Z
            DTEND:20261009T130000Z
            SUMMARY:Не сегодня
            END:VEVENT
            END:VCALENDAR
            """;

        var events = YandexIcsRoomScheduleProvider.ParseEvents(ics, At(0), At(0).AddDays(1));

        events.ShouldHaveSingleItem();
        events[0].Title.ShouldBe("Репетиция");
        events[0].Start.ShouldBe(At(15));
        events[0].End.ShouldBe(At(16, 30));
    }
}
