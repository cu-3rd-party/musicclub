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
[TestOf(typeof(CalendarSyncService))]
public class CalendarSyncServiceTests
{
    private const string CalendarUrl = "yandex-web://layer/1";

    private Mock<ICalDavOperations> _calDav = null!;
    private Mock<ICalendarIntegration> _integration = null!;
    private Mock<IRehearsalBookingRepository> _bookings = null!;
    private CalendarSyncService _service = null!;

    private readonly List<CalDavEventInfo> _created = [];

    private Mock<ISongRepository> _songs = null!;

    [Test]
    public async Task CreateBookingEvent_UsesSongTitleInEventTitle()
    {
        var song = new Domain.Entities.Song { Id = Guid.NewGuid(), Title = "Кино", Artist = "Группа крови" };
        _songs.Setup(r => r.FindByIdAsync(song.Id, It.IsAny<CancellationToken>())).ReturnsAsync(song);
        var booking = Booking(DateTimeOffset.UtcNow.AddDays(1));
        booking.SongId = song.Id;

        await _service.CreateBookingEventAsync(booking);

        _created.ShouldHaveSingleItem().Title.ShouldBe("🎸 Репетиция: Кино — Группа крови");
    }

    [SetUp]
    public void SetUp()
    {
        _created.Clear();

        _calDav = new Mock<ICalDavOperations>();
        _calDav.Setup(c => c.GetCalendarsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CalDavCalendarInfo(CalendarUrl, "MusicClub — Репетиции", null, null)]);
        _calDav.Setup(c => c.CreateEventAsync(CalendarUrl, It.IsAny<CalDavEventInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, CalDavEventInfo e, CancellationToken _) =>
            {
                _created.Add(e);
                return e with { Url = $"{CalendarUrl}/event/{_created.Count}/2026-10-07" };
            });

        _integration = new Mock<ICalendarIntegration>();
        _integration.SetupGet(i => i.IsSyncEnabled).Returns(true);

        _bookings = new Mock<IRehearsalBookingRepository>();
        foreach (var status in Enum.GetValues<BookingStatus>())
            _bookings.Setup(r => r.GetAllByStatusAsync(status, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var users = new Mock<IApplicationUserRepository>();
        users.Setup(r => r.FindByTgUserIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUser { Id = Guid.NewGuid(), DisplayName = "Иван", YandexLogin = "ivan" });

        _songs = new Mock<ISongRepository>();

        _service = new CalendarSyncService(_calDav.Object, _integration.Object, users.Object, _bookings.Object,
            _songs.Object, NullLogger<CalendarSyncService>.Instance);
    }

    private static RehearsalBooking Booking(DateTimeOffset scheduledAt,
        BookingStatus status = BookingStatus.Confirmed, string? url = null)
    {
        return new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            ScheduledAt = scheduledAt,
            DurationMinutes = 80,
            Status = status,
            RequesterTgUserId = 42,
            CalDavEventUrl = url
        };
    }

    private void GivenBookings(BookingStatus status, params RehearsalBooking[] bookings)
    {
        _bookings.Setup(r => r.GetAllByStatusAsync(status, It.IsAny<CancellationToken>())).ReturnsAsync(bookings);
    }

    [Test]
    public async Task CreateBookingEvent_WhenSyncDisabled_KeepsBookingOnlyInBot()
    {
        _integration.SetupGet(i => i.IsSyncEnabled).Returns(false);
        var booking = Booking(DateTimeOffset.UtcNow.AddDays(1));

        await _service.CreateBookingEventAsync(booking);

        booking.CalDavEventUrl.ShouldBeNull();
        _calDav.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateBookingEvent_WhenAlreadySynced_DoesNotDuplicate()
    {
        var booking = Booking(DateTimeOffset.UtcNow.AddDays(1), url: "existing");

        await _service.CreateBookingEventAsync(booking);

        _created.ShouldBeEmpty();
    }

    [Test]
    public async Task DeleteBookingEvent_WhenSyncDisabled_KeepsUrlForLaterBackfill()
    {
        _integration.SetupGet(i => i.IsSyncEnabled).Returns(false);
        var booking = Booking(DateTimeOffset.UtcNow, BookingStatus.Cancelled, "existing");

        await _service.DeleteBookingEventAsync(booking);

        booking.CalDavEventUrl.ShouldBe("existing");
        _calDav.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Backfill_WhenSyncDisabled_DoesNothing()
    {
        _integration.SetupGet(i => i.IsSyncEnabled).Returns(false);
        GivenBookings(BookingStatus.Confirmed, Booking(DateTimeOffset.UtcNow.AddDays(-3)));

        var result = await _service.BackfillMissingEventsAsync();

        result.ShouldBe(new CalendarBackfillResult(0, 0, 0));
        _calDav.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Backfill_CreatesMissingEvents_PastOnesWithoutInvites()
    {
        var past = Booking(DateTimeOffset.UtcNow.AddDays(-10));
        var future = Booking(DateTimeOffset.UtcNow.AddDays(2));
        var synced = Booking(DateTimeOffset.UtcNow.AddDays(3), url: "existing");
        GivenBookings(BookingStatus.Confirmed, past, future, synced);

        var result = await _service.BackfillMissingEventsAsync();

        result.ShouldBe(new CalendarBackfillResult(2, 0, 0));
        past.CalDavEventUrl.ShouldNotBeNull();
        future.CalDavEventUrl.ShouldNotBeNull();
        _created[0].Participants.ShouldBeEmpty();
        _created[1].Participants.ShouldNotBeEmpty();
        _bookings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Backfill_DeletesEventsOfCancelledBookings_AndRetriesFailures()
    {
        var cancelled = Booking(DateTimeOffset.UtcNow, BookingStatus.Cancelled, "cancelled-url");
        var rejected = Booking(DateTimeOffset.UtcNow, BookingStatus.Rejected, "rejected-url");
        GivenBookings(BookingStatus.Cancelled, cancelled);
        GivenBookings(BookingStatus.Rejected, rejected);
        _calDav.Setup(c => c.DeleteEventAsync("rejected-url", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Яндекс недоступен"));

        var result = await _service.BackfillMissingEventsAsync();

        result.ShouldBe(new CalendarBackfillResult(0, 1, 1));
        cancelled.CalDavEventUrl.ShouldBeNull();
        rejected.CalDavEventUrl.ShouldBe("rejected-url");
    }

    [Test]
    public async Task Backfill_WhenOneCreateFails_ContinuesWithOthers()
    {
        var broken = Booking(DateTimeOffset.UtcNow.AddDays(1));
        var ok = Booking(DateTimeOffset.UtcNow.AddDays(2));
        GivenBookings(BookingStatus.Confirmed, broken, ok);
        _calDav.Setup(c => c.CreateEventAsync(CalendarUrl,
                It.Is<CalDavEventInfo>(e => e.Uid.Contains(broken.Id.ToString())), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _service.BackfillMissingEventsAsync();

        result.ShouldBe(new CalendarBackfillResult(1, 0, 1));
        broken.CalDavEventUrl.ShouldBeNull();
        ok.CalDavEventUrl.ShouldNotBeNull();
    }
}
