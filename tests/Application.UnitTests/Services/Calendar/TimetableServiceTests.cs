using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(TimetableService))]
public class TimetableServiceTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 5, 0, 0, 0, TimeSpan.FromHours(3));
    private static readonly DateTimeOffset To = From.AddDays(7);

    private readonly ApplicationUser _user = new()
    {
        Id = Guid.NewGuid(), TgUserId = 42, DisplayName = "Иван", YandexLogin = "ivan"
    };

    private readonly Guid _mySongId = Guid.NewGuid();
    private readonly Guid _otherSongId = Guid.NewGuid();

    private Mock<ICalendarEventRepository> _events = null!;
    private Mock<IRehearsalBookingRepository> _bookings = null!;
    private Mock<IExternalScheduleProvider> _external = null!;
    private TimetableService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _events = new Mock<ICalendarEventRepository>();
        _events.Setup(r => r.GetUserActiveEventsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _events.Setup(r => r.GetActiveClubEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _bookings = new Mock<IRehearsalBookingRepository>();
        _bookings.Setup(r => r.GetActiveInRangeAsync(From, To, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var assignments = new Mock<ISongRoleAssignmentRepository>();
        assignments.Setup(r => r.GetSongIdsByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_mySongId]);

        var songs = new Mock<ISongRepository>();
        songs.Setup(r => r.GetTitlesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_mySongId] = "Моя песня", [_otherSongId] = "Чужая песня" });

        var users = new Mock<IApplicationUserRepository>();
        users.Setup(r => r.FindByIdAsync(It.IsAny<object[]>())).ReturnsAsync(_user);

        _external = new Mock<IExternalScheduleProvider>();
        _external.Setup(p => p.GetUserEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _service = new TimetableService(_events.Object, _bookings.Object, assignments.Object, songs.Object,
            users.Object, _external.Object, NullLogger<TimetableService>.Instance);
    }

    private RehearsalBooking Booking(Guid? songId, long requester = 1, Guid? coach = null, int dayOffset = 1)
    {
        return new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            SongId = songId,
            RequesterTgUserId = requester,
            CoachUserId = coach,
            ScheduledAt = From.AddDays(dayOffset).AddHours(18),
            DurationMinutes = 80,
            Status = BookingStatus.Confirmed
        };
    }

    [Test]
    public async Task Mine_IncludesOnlyBookingsRelatedToUser()
    {
        var mySong = Booking(_mySongId);
        var myRequest = Booking(null, 42);
        var asCoach = Booking(_otherSongId, coach: _user.Id);
        var foreign = Booking(_otherSongId);
        _bookings.Setup(r => r.GetActiveInRangeAsync(From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mySong, myRequest, asCoach, foreign]);

        var result = await _service.GetTimetableAsync(_user.Id, TimetableScope.Mine, From, To);

        result.Select(e => e.Id).ShouldBe(
            [$"booking:{mySong.Id}", $"booking:{myRequest.Id}", $"booking:{asCoach.Id}"], true);
        result.Single(e => e.Id == $"booking:{mySong.Id}").Title.ShouldBe("Моя песня");
        result.Single(e => e.Id == $"booking:{myRequest.Id}").Title.ShouldBe("Репетиция");
    }

    [Test]
    public async Task Mine_AddsYandexEventsAndSkipsDuplicatesOfRehearsals()
    {
        var booking = Booking(_mySongId);
        _bookings.Setup(r => r.GetActiveInRangeAsync(From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync([booking]);
        _external.Setup(p => p.GetUserEventsAsync("ivan@edu.centraluniversity.ru", From, To,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExternalScheduleEvent("🎸 Репетиция", booking.ScheduledAt, booking.ScheduledAt.AddMinutes(80)),
                new ExternalScheduleEvent("Матанализ", From.AddHours(10), From.AddHours(11)),
                new ExternalScheduleEvent(null, From.AddHours(12), From.AddHours(13))
            ]);

        var result = await _service.GetTimetableAsync(_user.Id, TimetableScope.Mine, From, To);

        result.Count.ShouldBe(3);
        result.Where(e => e.Kind == TimetableEventKind.External).Select(e => e.Title)
            .ShouldBe(["Матанализ", "Занят"]);
    }

    [Test]
    public async Task Mine_WhenYandexFails_ReturnsOtherEvents()
    {
        _bookings.Setup(r => r.GetActiveInRangeAsync(From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(_mySongId)]);
        _external.Setup(p => p.GetUserEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cookies протухли"));

        var result = await _service.GetTimetableAsync(_user.Id, TimetableScope.Mine, From, To);

        result.Count.ShouldBe(1);
    }

    [Test]
    public async Task Club_ReturnsAllBookingsAndDeduplicatesTracklistCopies()
    {
        _bookings.Setup(r => r.GetActiveInRangeAsync(From, To, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Booking(_mySongId), Booking(_otherSongId)]);
        var sourceId = Guid.NewGuid();
        var start = new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        _events.Setup(r => r.GetActiveClubEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new CalendarEvent { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Концерт", StartAt = start,
                    EndAt = start.AddHours(2), EventType = CalendarEventType.Performance, SourceType = "tracklist",
                    SourceId = sourceId },
                new CalendarEvent { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "Концерт", StartAt = start,
                    EndAt = start.AddHours(2), EventType = CalendarEventType.Performance, SourceType = "tracklist",
                    SourceId = sourceId }
            ]);

        var result = await _service.GetTimetableAsync(_user.Id, TimetableScope.Club, From, To);

        result.Count(e => e.Kind == TimetableEventKind.Rehearsal).ShouldBe(2);
        result.Single(e => e.Kind == TimetableEventKind.Performance).CanDelete.ShouldBeFalse();
        _external.Verify(p => p.GetUserEventsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RangeTooLong_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            _service.GetTimetableAsync(_user.Id, TimetableScope.Club, From, From.AddDays(60)));
    }
}
