using CuMusicClub.Web.Bot;

// Отдельный namespace: эти тесты не требуют БД и не должны поднимать FunctionalTestSetup
namespace CuMusicClub.Web.UnitTests.Bot;

public class BotDateParserTests
{
    // Среда
    private static readonly DateTime Today = new(2026, 10, 7);

    [TestCase("12.10", 2026, 10, 12)]
    [TestCase("12/10", 2026, 10, 12)]
    [TestCase("12-10", 2026, 10, 12)]
    [TestCase("1.1.2027", 2027, 1, 1)]
    [TestCase("сегодня", 2026, 10, 7)]
    [TestCase("завтра", 2026, 10, 8)]
    [TestCase("ср", 2026, 10, 7)]
    [TestCase("пт", 2026, 10, 9)]
    [TestCase("Пн", 2026, 10, 12)]
    [TestCase("пятницу", 2026, 10, 9)]
    [TestCase("05.01", 2027, 1, 5)]
    [TestCase("06.10", 2026, 10, 6)]
    public void TryParseDate_Valid(string input, int year, int month, int day)
    {
        BotDateParser.TryParseDate(input, Today, out var date).ShouldBeTrue();
        date.ShouldBe(new DateTime(year, month, day));
    }

    [TestCase("31.02")]
    [TestCase("00.10")]
    [TestCase("12.13")]
    [TestCase("abc")]
    [TestCase("")]
    public void TryParseDate_Invalid(string input)
    {
        BotDateParser.TryParseDate(input, Today, out _).ShouldBeFalse();
    }

    [TestCase("18:00", 18, 0)]
    [TestCase("18", 18, 0)]
    [TestCase("9:30", 9, 30)]
    [TestCase("1830", 18, 30)]
    [TestCase("18.30", 18, 30)]
    [TestCase("17-19", 17, 0)]
    [TestCase("17:30–19:00", 17, 30)]
    public void TryParseTime_Valid(string input, int hour, int minute)
    {
        BotDateParser.TryParseTime(input, out var time).ShouldBeTrue();
        time.ShouldBe(new TimeOnly(hour, minute));
    }

    [TestCase("25:00")]
    [TestCase("18:60")]
    [TestCase("вечером")]
    public void TryParseTime_Invalid(string input)
    {
        BotDateParser.TryParseTime(input, out _).ShouldBeFalse();
    }

    [Test]
    public void TryParseDateTime_RequiresBothParts()
    {
        BotDateParser.TryParseDateTime("19.10", Today, out _).ShouldBeFalse();
        BotDateParser.TryParseDateTime("  19.10   18:30 ", Today, out var value).ShouldBeTrue();
        value.ShouldBe(new DateTime(2026, 10, 19, 18, 30, 0));
    }

    [TestCase("пт 19", 19, 0, null, null)]
    [TestCase("пт 19:30", 19, 30, null, null)]
    [TestCase("пт 19-22", 19, 0, 22, 0)]
    [TestCase("пт 19:00–20:30", 19, 0, 20, 30)]
    [TestCase("пт 19:00 - 20:30", 19, 0, 20, 30)]
    [TestCase("пт 19 22", 19, 0, 22, 0)]
    [TestCase("пт 1900-2130", 19, 0, 21, 30)]
    public void TryParseSlot_Valid(string input, int startHour, int startMinute, int? endHour, int? endMinute)
    {
        BotDateParser.TryParseSlot(input, Today, out var start, out var end).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 10, 9, startHour, startMinute, 0));
        end.ShouldBe(endHour is { } h ? new DateTime(2026, 10, 9, h, endMinute!.Value, 0) : null);
    }

    [TestCase("пт")]
    [TestCase("пт 22-19")]
    [TestCase("пт 19-19")]
    [TestCase("пт 19-25")]
    [TestCase("пт вечером")]
    [TestCase("19-22")]
    public void TryParseSlot_Invalid(string input)
    {
        BotDateParser.TryParseSlot(input, Today, out _, out _).ShouldBeFalse();
    }

    [Test]
    public void FormatDayHeader_IsRussian()
    {
        BotDateParser.FormatDayHeader(new DateTime(2026, 10, 12)).ShouldBe("Пн, 12.10");
    }
}
