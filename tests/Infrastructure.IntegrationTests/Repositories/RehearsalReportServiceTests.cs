using ClosedXML.Excel;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Infrastructure.IntegrationTests.Repositories;

[TestFixture]
public class RehearsalReportServiceTests : TestBase
{
    private static readonly DateOnly October = new(2026, 10, 1);
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);

    [Test]
    public async Task Process_StoresReportBlobAndDelivery_AndSendsOnce()
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<IApplicationUserRepository>();
        var songs = provider.GetRequiredService<ISongRepository>();

        var member = new ApplicationUser { DisplayName = "M", UserName = "member", YandexLogin = "m.login", TgUserId = 11 };
        var boss = new ApplicationUser { DisplayName = "B", UserName = "IgaMamaev", TgUserId = 22 };
        await users.AddAsync(member);
        await users.AddAsync(boss);
        await users.SaveChangesAsync();

        var song = new Song { Title = "Мы", Artist = "Дайте Танк", LinkUrl = "https://example.com" };
        var role = new SongRole { Song = song, RoleTitle = "Гитара" };
        role.Assignment = new SongRoleAssignment { Song = song, SongRole = role, UserId = member.Id };
        song.Roles.Add(role);
        await songs.AddAsync(song);
        await songs.SaveChangesAsync();

        var slot = new DateTimeOffset(2026, 10, 3, 17, 20, 0, Msk);
        var room = new Mock<IRoomScheduleProvider>();
        room.SetupGet(r => r.IsConfigured).Returns(true);
        room.Setup(r => r.GetRoomEventsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExternalScheduleEvent("Репетиция Мы — Дайте Танк", slot, slot.AddMinutes(80))]);
        var telegram = new Mock<ITelegramChatService>();

        var reports = provider.GetRequiredService<IRehearsalReportRepository>();
        var service = new RehearsalReportService(
            reports,
            room.Object,
            songs,
            provider.GetRequiredService<ISongRoadieRepository>(),
            provider.GetRequiredService<IRehearsalBookingRepository>(),
            users,
            telegram.Object,
            Options.Create(new RehearsalReportOptions { Recipients = ["igamamaev"], FirstPeriod = October }),
            NullLogger<RehearsalReportService>.Instance);

        var now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, Msk);
        await service.ProcessDueReportsAsync(now);
        await service.ProcessDueReportsAsync(now);

        telegram.Verify(t => t.SendDirectDocument(22, It.IsAny<byte[]>(),
            "musicclub_rehearsals_2026-10-01_2026-10-31.xlsx", It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        using var readScope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<IRehearsalReportRepository>()
            .FindByPeriodStartAsync(October);
        stored.ShouldNotBeNull();
        stored.PeriodEnd.ShouldBe(new DateOnly(2026, 10, 31));
        var delivery = stored.Deliveries.ShouldHaveSingleItem();
        delivery.Recipient.ShouldBe("igamamaev");
        delivery.TgUserId.ShouldBe(22);

        using var workbook = new XLWorkbook(new MemoryStream(stored.Content));
        var sheet = workbook.Worksheet(1);
        sheet.Cell("B2").GetString().ShouldBe("m.login");
        sheet.Cell("C2").GetString().ShouldBe("03.10.2026 17:20");
    }
}
