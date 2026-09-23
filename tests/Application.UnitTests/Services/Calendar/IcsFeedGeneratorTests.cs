using System.Text.RegularExpressions;
using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Enums;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(IcsFeedGenerator))]
public class IcsFeedGeneratorTests
{
    private IcsFeedGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new IcsFeedGenerator();
    }

    #region GenerateIcs - Basic Structure

    [Test]
    public void GenerateIcs_ProducesValidVCalendarHeader()
    {
        // Arrange
        var events = new List<CalendarEventDto>();
        var productName = "-//Music Club//Calendar//EN";
        var calendarName = "Music Club Calendar";

        // Act
        var ics = _generator.GenerateIcs(events, productName, calendarName);

        // Assert
        ics.ShouldContain("BEGIN:VCALENDAR");
        ics.ShouldContain("VERSION:2.0");
        ics.ShouldContain($"PRODID:{productName}");
        ics.ShouldContain($"X-WR-CALNAME:{calendarName}");
        ics.ShouldContain("CALSCALE:GREGORIAN");
        ics.ShouldContain("METHOD:PUBLISH");
        ics.ShouldContain("END:VCALENDAR");
    }

    [Test]
    public void GenerateIcs_WithSingleEvent_ProducesValidVEvent()
    {
        // Arrange
        var startAt = new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);
        var endAt = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "Test Event",
                Description = "Test Description",
                StartAt = startAt,
                EndAt = endAt,
                Location = "Test Location",
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual",
                SourceId = null
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldContain("BEGIN:VEVENT");
        ics.ShouldContain("UID:");
        ics.ShouldContain("DTSTAMP:");
        ics.ShouldContain("DTSTART:20260923T100000Z");
        ics.ShouldContain("DTEND:20260923T120000Z");
        ics.ShouldContain("SUMMARY:Test Event");
        ics.ShouldContain("DESCRIPTION:Test Description");
        ics.ShouldContain("LOCATION:Test Location");
        ics.ShouldContain("CATEGORIES:REHEARSAL");
        ics.ShouldContain("END:VEVENT");
    }

    [Test]
    public void GenerateIcs_WithMultipleEvents_ProducesMultipleVEvents()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            CreateEvent("Event 1", CalendarEventType.Rehearsal),
            CreateEvent("Event 2", CalendarEventType.Performance),
            CreateEvent("Event 3", CalendarEventType.Personal)
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        var eventCount = Regex.Matches(ics, "BEGIN:VEVENT").Count;
        eventCount.ShouldBe(3);

        ics.ShouldContain("SUMMARY:Event 1");
        ics.ShouldContain("SUMMARY:Event 2");
        ics.ShouldContain("SUMMARY:Event 3");
        
        ics.ShouldContain("CATEGORIES:REHEARSAL");
        ics.ShouldContain("CATEGORIES:PERFORMANCE");
        ics.ShouldContain("CATEGORIES:PERSONAL");
    }

    #endregion

    #region GenerateIcs - Text Escaping

    [Test]
    public void GenerateIcs_EscapesCommasInText()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "Event, with comma",
                Description = "Description, also with comma",
                Location = "Moscow, Russia",
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldContain("SUMMARY:Event\\, with comma");
        ics.ShouldContain("DESCRIPTION:Description\\, also with comma");
        ics.ShouldContain("LOCATION:Moscow\\, Russia");
    }

    [Test]
    public void GenerateIcs_EscapesSemicolonsInText()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "Event; with semicolon",
                Description = "Description; also with semicolon",
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldContain("SUMMARY:Event\\; with semicolon");
        ics.ShouldContain("DESCRIPTION:Description\\; also with semicolon");
    }

    [Test]
    public void GenerateIcs_EscapesBackslashesInText()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = @"Event\with\backslash",
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldContain("SUMMARY:Event\\\\with\\\\backslash");
    }

    [Test]
    public void GenerateIcs_EscapesNewlinesInText()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "Event\nwith\nnewlines",
                Description = "Line 1\r\nLine 2",
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldContain("SUMMARY:Event\\nwith\\nnewlines");
        ics.ShouldContain("DESCRIPTION:Line 1\\nLine 2");
    }

    [Test]
    public void GenerateIcs_HandlesNullDescription()
    {
        // Arrange
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "Event without description",
                Description = null,
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        ics.ShouldNotContain("DESCRIPTION:");
        ics.ShouldContain("SUMMARY:Event without description");
    }

    #endregion

    #region GenerateIcs - Date-Time Formatting

    [Test]
    public void GenerateIcs_FormatsDateTimeInUtc()
    {
        // Arrange
        var startAt = new DateTimeOffset(2026, 1, 15, 14, 30, 45, TimeSpan.FromHours(3)); // UTC+3
        var events = new List<CalendarEventDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Title = "UTC Test",
                StartAt = startAt,
                EndAt = startAt.AddHours(1),
                EventType = CalendarEventType.Rehearsal,
                SourceType = "manual"
            }
        };

        // Act
        var ics = _generator.GenerateIcs(events, "-//Test//EN", "Test Calendar");

        // Assert
        // 14:30 UTC+3 = 11:30 UTC
        ics.ShouldContain("DTSTART:20260115T113045Z");
    }

    #endregion

    #region Helpers

    private static CalendarEventDto CreateEvent(string title, CalendarEventType eventType)
    {
        return new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Title = title,
            Description = $"Description for {title}",
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Location = "Test Location",
            EventType = eventType,
            SourceType = "manual",
            SourceId = null
        };
    }

    #endregion
}
