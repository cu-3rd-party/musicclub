using ClosedXML.Excel;
using CuMusicClub.Application.Services.Calendar;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(RehearsalReportBuilder))]
public class RehearsalReportBuilderTests
{
    private static readonly DateTimeOffset Slot = new(2026, 10, 10, 17, 30, 0, TimeSpan.FromHours(3));

    private static readonly Dictionary<DateTimeOffset, string> NoRequesters = [];

    private static ExternalScheduleEvent Event(
        string title,
        string? organizer = null,
        params ExternalScheduleAttendee[] attendees)
    {
        return new ExternalScheduleEvent(title, Slot, Slot.AddMinutes(80), organizer, attendees);
    }

    private static string[] Logins(IEnumerable<RehearsalReportRow> rows)
    {
        return rows.Select(r => r.Login ?? "").ToArray();
    }

    [Test]
    public void BuildRows_SkipsNonRehearsalEvents()
    {
        var song = new RehearsalReportSong("BUSY", "", ["a"], null);

        RehearsalReportBuilder.BuildRows([Event("BUSY")], [song], NoRequesters).ShouldBeEmpty();
    }

    [TestCase("Репетиция Мы — Дайте Танк")]
    [TestCase("🎸 Репетиция: Мы — Дайте Танк")]
    [TestCase("🎸 Репетиция:  мы  —  дайте танк ")]
    public void BuildRows_WithoutAttendees_UsesSongRoster(string title)
    {
        var song = new RehearsalReportSong("Мы", "Дайте Танк", ["b", "a"], "roadie");

        var rows = RehearsalReportBuilder.BuildRows([Event(title, "old.bot")], [song], NoRequesters);

        Logins(rows).ShouldBe(["a", "b"]);
    }

    [Test]
    public void BuildRows_WithAttendees_KeepsFormerMemberAndDropsNonParticipants()
    {
        var song = new RehearsalReportSong("Мы", "Дайте Танк", ["member", "joined.later"], "roadie");
        var roomEvent = Event("🎸 Репетиция: Мы — Дайте Танк", "organizer",
            new ExternalScheduleAttendee("organizer", false),
            new ExternalScheduleAttendee("requester", false),
            new ExternalScheduleAttendee("roadie", false),
            new ExternalScheduleAttendee("member", false),
            new ExternalScheduleAttendee("former.member", false),
            new ExternalScheduleAttendee("declined", true));

        var rows = RehearsalReportBuilder.BuildRows([roomEvent], [song],
            new Dictionary<DateTimeOffset, string> { [Slot] = "requester" });

        Logins(rows).ShouldBe(["former.member", "member"]);
    }

    [Test]
    public void BuildRows_KeepsOrganizerAndRoadieWhoAreSongMembers()
    {
        var song = new RehearsalReportSong("Мы", "Дайте Танк", ["organizer", "roadie"], "roadie");
        var roomEvent = Event("🎸 Репетиция: Мы — Дайте Танк", "organizer",
            new ExternalScheduleAttendee("organizer", false),
            new ExternalScheduleAttendee("roadie", false));

        var rows = RehearsalReportBuilder.BuildRows([roomEvent], [song], NoRequesters);

        Logins(rows).ShouldBe(["organizer", "roadie"]);
    }

    [Test]
    public void BuildRows_NoKnownLogins_KeepsSlotWithBlankLogin()
    {
        var unmatched = new List<string>();

        var rows = RehearsalReportBuilder.BuildRows([Event("Репетиция Удалённая — песня")], [], NoRequesters, unmatched);

        rows.ShouldHaveSingleItem().Login.ShouldBeNull();
        unmatched.ShouldBe(["Репетиция Удалённая — песня"]);
    }

    [Test]
    public void BuildWorkbook_MatchesReportFormat()
    {
        var content = RehearsalReportBuilder.BuildWorkbook(
            [new RehearsalReportRow("a.ivanov", Slot), new RehearsalReportRow(null, Slot.ToUniversalTime())],
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31));

        using var workbook = new XLWorkbook(new MemoryStream(content));
        var sheet = workbook.Worksheets.ShouldHaveSingleItem();
        sheet.Name.ShouldBe("musicclub_rehearsals_2026-10-01");
        sheet.Cell("A1").GetString().ShouldBe("Club");
        sheet.Cell("B1").GetString().ShouldBe("login");
        sheet.Cell("C1").GetString().ShouldBe("datetime");
        sheet.Cell("A2").GetString().ShouldBe("MusicClub");
        sheet.Cell("B2").GetString().ShouldBe("a.ivanov");
        sheet.Cell("C2").Value.IsText.ShouldBeTrue();
        sheet.Cell("C2").GetString().ShouldBe("10.10.2026 17:30");
        sheet.Cell("B3").IsEmpty().ShouldBeTrue();
        sheet.Cell("C3").GetString().ShouldBe("10.10.2026 17:30");
    }
}
