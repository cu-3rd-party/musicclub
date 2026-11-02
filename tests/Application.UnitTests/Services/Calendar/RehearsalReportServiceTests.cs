using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Application.UnitTests.Common;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(RehearsalReportService))]
public class RehearsalReportServiceTests
{
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, Msk);

    [TestCase("2026-11-01T09:59:00+03:00", "2026-11-01T10:00:00+03:00")]
    [TestCase("2026-11-01T10:00:00+03:00", "2026-12-01T10:00:00+03:00")]
    [TestCase("2026-12-15T00:00:00+03:00", "2027-01-01T10:00:00+03:00")]
    [TestCase("2026-10-31T22:00:00+00:00", "2026-11-01T10:00:00+03:00")]
    public void NextSendTime_IsFirstOfMonthAtTenMoscow(string now, string expected)
    {
        RehearsalReportService.NextSendTime(DateTimeOffset.Parse(now)).ShouldBe(DateTimeOffset.Parse(expected));
    }

    [TestCase("2026-11-01T09:59:00+03:00", 0)]
    [TestCase("2026-11-01T10:00:00+03:00", 1)]
    [TestCase("2026-11-02T12:00:00+03:00", 1)]
    [TestCase("2027-02-01T10:00:00+03:00", 4)]
    public void GetDuePeriods_CatchesUpFromFirstPeriod(string now, int expectedCount)
    {
        var periods = RehearsalReportService.GetDuePeriods(October, DateTimeOffset.Parse(now)).ToList();

        periods.Count.ShouldBe(expectedCount);
        if (expectedCount > 0)
            periods[0].ShouldBe(October);
    }

    private Mock<IRehearsalReportRepository> _reports = null!;
    private Mock<IRoomScheduleProvider> _room = null!;
    private Mock<IApplicationUserRepository> _users = null!;
    private Mock<ITelegramChatService> _telegram = null!;
    private RehearsalReportService _service = null!;
    private List<RehearsalReport> _savedReports = null!;
    private List<RehearsalReportDelivery> _savedDeliveries = null!;
    private readonly Dictionary<DateOnly, IReadOnlySet<string>> _delivered = [];

    [SetUp]
    public void SetUp()
    {
        _delivered.Clear();
        _savedReports = [];
        _savedDeliveries = [];

        _reports = new Mock<IRehearsalReportRepository>();
        _reports.Setup(r => r.GetDeliveredRecipientsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_delivered);
        _reports.Setup(r => r.AddAsync(It.IsAny<RehearsalReport>(), It.IsAny<CancellationToken>()))
            .Callback<RehearsalReport, CancellationToken>((r, _) => _savedReports.Add(r))
            .Returns(Task.CompletedTask);
        _reports.Setup(r => r.AddDeliveryAsync(It.IsAny<RehearsalReportDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<RehearsalReportDelivery, CancellationToken>((d, _) => _savedDeliveries.Add(d))
            .Returns(Task.CompletedTask);

        _room = new Mock<IRoomScheduleProvider>();
        _room.SetupGet(r => r.IsConfigured).Returns(true);
        _room.Setup(r => r.GetRoomEventsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _users = new Mock<IApplicationUserRepository>();
        _users.Setup(r => r.Query()).Returns(new[]
        {
            new ApplicationUser { Id = Guid.NewGuid(), UserName = "IgaMamaev", TgUserId = 100, DisplayName = "И" }
        }.AsAsyncQueryable());

        var songs = new Mock<ISongRepository>();
        songs.Setup(r => r.Query()).Returns(Array.Empty<Domain.Entities.Song>().AsAsyncQueryable());
        var roadies = new Mock<ISongRoadieRepository>();
        roadies.Setup(r => r.Query()).Returns(Array.Empty<SongRoadie>().AsAsyncQueryable());
        var bookings = new Mock<IRehearsalBookingRepository>();
        bookings.Setup(r => r.GetActiveInRangeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        _telegram = new Mock<ITelegramChatService>();

        _service = new RehearsalReportService(_reports.Object, _room.Object, songs.Object, roadies.Object,
            bookings.Object, _users.Object, _telegram.Object,
            Options.Create(new RehearsalReportOptions
            {
                Enabled = true, Recipients = ["yaroslavakir", "@igamamaev"], FirstPeriod = October
            }),
            NullLogger<RehearsalReportService>.Instance);
    }

    [Test]
    public async Task Process_CreatesReportAndSkipsUnknownRecipient()
    {
        await _service.ProcessDueReportsAsync(Now);

        var report = _savedReports.ShouldHaveSingleItem();
        report.PeriodStart.ShouldBe(October);
        report.PeriodEnd.ShouldBe(new DateOnly(2026, 10, 31));
        report.FileName.ShouldBe("musicclub_rehearsals_2026-10-01_2026-10-31.xlsx");
        report.Content.ShouldNotBeEmpty();

        _room.Verify(r => r.GetRoomEventsAsync(
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, Msk),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, Msk),
            It.IsAny<CancellationToken>()), Times.Once);

        _telegram.Verify(t => t.SendDirectDocument(100, report.Content, report.FileName, It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        var delivery = _savedDeliveries.ShouldHaveSingleItem();
        delivery.Recipient.ShouldBe("igamamaev");
        delivery.TgUserId.ShouldBe(100);
    }

    [Test]
    public async Task Process_ExistingReport_ResendsStoredFileWithoutRebuilding()
    {
        var stored = new RehearsalReport
        {
            Id = Guid.NewGuid(), PeriodStart = October, FileName = "stored.xlsx", Content = [1, 2, 3]
        };
        _reports.Setup(r => r.FindByPeriodStartAsync(October, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        await _service.ProcessDueReportsAsync(Now);

        _savedReports.ShouldBeEmpty();
        _room.Verify(r => r.GetRoomEventsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _telegram.Verify(t => t.SendDirectDocument(100, stored.Content, "stored.xlsx", It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _savedDeliveries.ShouldHaveSingleItem().ReportId.ShouldBe(stored.Id);
    }

    [Test]
    public async Task Process_AllDelivered_DoesNothing()
    {
        _delivered[October] = new HashSet<string> { "yaroslavakir", "igamamaev" };

        await _service.ProcessDueReportsAsync(Now);

        _reports.Verify(r => r.FindByPeriodStartAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        _telegram.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Process_SendFails_DoesNotRecordDelivery()
    {
        _telegram.Setup(t => t.SendDirectDocument(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("403"));

        await _service.ProcessDueReportsAsync(Now);

        _savedReports.Count.ShouldBe(1);
        _savedDeliveries.ShouldBeEmpty();
    }

    [Test]
    public async Task Process_CalendarNotConfigured_DoesNotStoreEmptyReport()
    {
        _room.SetupGet(r => r.IsConfigured).Returns(false);

        await Should.ThrowAsync<InvalidOperationException>(() => _service.ProcessDueReportsAsync(Now));

        _savedReports.ShouldBeEmpty();
    }

    [Test]
    public async Task Process_BeforeSendTime_DoesNothing()
    {
        await _service.ProcessDueReportsAsync(new DateTimeOffset(2026, 11, 1, 9, 59, 0, Msk));

        _savedReports.ShouldBeEmpty();
    }
}
