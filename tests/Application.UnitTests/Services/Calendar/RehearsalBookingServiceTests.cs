using CuMusicClub.Application.Common.Exceptions;
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
[TestOf(typeof(RehearsalBookingService))]
public class RehearsalBookingServiceTests
{
    private const long RequesterTgId = 1;
    private const long BandmateTgId = 2;
    private const long StrangerTgId = 3;
    private const long UnknownTgId = 4;

    private static readonly DateTime ScheduledMsk = new(2026, 10, 5, 18, 0, 0);

    private readonly Guid _songId = Guid.NewGuid();
    private readonly ApplicationUser _bandmate = new() { Id = Guid.NewGuid(), TgUserId = BandmateTgId, DisplayName = "Петя" };
    private readonly ApplicationUser _stranger = new() { Id = Guid.NewGuid(), TgUserId = StrangerTgId, DisplayName = "Вася" };

    private RehearsalBooking _booking = null!;
    private RehearsalBookingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _booking = new RehearsalBooking
        {
            Id = Guid.NewGuid(),
            ScheduledAt = new DateTimeOffset(ScheduledMsk, TimeSpan.FromHours(3)).ToUniversalTime(),
            Status = BookingStatus.Confirmed,
            RequesterTgUserId = RequesterTgId,
            SongId = _songId
        };

        var bookings = new Mock<IRehearsalBookingRepository>();
        bookings.Setup(r => r.GetActiveInRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(() => [_booking]);

        var assignments = new Mock<ISongRoleAssignmentRepository>();
        assignments.Setup(r => r.GetMemberUserIdsBySongIdAsync(_songId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_bandmate.Id]);

        var users = new Mock<IApplicationUserRepository>();
        users.Setup(r => r.FindByTgUserIdAsync(BandmateTgId, It.IsAny<CancellationToken>())).ReturnsAsync(_bandmate);
        users.Setup(r => r.FindByTgUserIdAsync(StrangerTgId, It.IsAny<CancellationToken>())).ReturnsAsync(_stranger);
        users.Setup(r => r.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _service = new RehearsalBookingService(bookings.Object, Mock.Of<ICalendarSyncService>(), assignments.Object,
            users.Object, new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock }.Object,
            NullLogger<RehearsalBookingService>.Instance);
    }

    [TestCase(RequesterTgId)]
    [TestCase(BandmateTgId)]
    [TestCase(StrangerTgId)]
    public async Task CancelBookingAsync_AnyKnownUser_Cancels(long tgUserId)
    {
        var result = await _service.CancelBookingAsync(tgUserId, ScheduledMsk);

        result.Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Test]
    public async Task CancelBookingAsync_UnknownUser_Throws()
    {
        await Should.ThrowAsync<BookingRuleException>(() => _service.CancelBookingAsync(UnknownTgId, ScheduledMsk));
        _booking.Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Test]
    public async Task CancelBookingAsync_WithoutSong_OnlyRequesterOrOrganizer()
    {
        _booking.SongId = null;

        await Should.ThrowAsync<BookingRuleException>(() => _service.CancelBookingAsync(StrangerTgId, ScheduledMsk));
        (await _service.CancelBookingAsync(RequesterTgId, ScheduledMsk)).Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Test]
    public async Task CreateBookingAsync_NotSongMember_Books()
    {
        var at = DateTime.UtcNow.AddHours(3).Date.AddDays(2).AddHours(19);

        var booking = await _service.CreateBookingAsync(StrangerTgId, at, _songId, 180);

        booking.Status.ShouldBe(BookingStatus.Confirmed);
        booking.DurationMinutes.ShouldBe(180);
    }

    [Test]
    public async Task CreateBookingAsync_WithoutSong_RequiresOrganizer()
    {
        var at = DateTime.UtcNow.AddHours(3).Date.AddDays(2).AddHours(19);

        await Should.ThrowAsync<ForbiddenAccessException>(() => _service.CreateBookingAsync(StrangerTgId, at, null));
    }
}
