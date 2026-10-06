using System.Text.Json;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Infrastructure.Yandex;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(YandexWebCalendarOperations))]
public class YandexWebCalendarOperationsTests
{
    private const long LayerId = 36383467;
    private const string CalendarUrl = "yandex-web://layer/36383467";

    private FakeMayaClient _maya = null!;
    private YandexWebCalendarOperations _operations = null!;

    [SetUp]
    public void SetUp()
    {
        _maya = new FakeMayaClient();
        _operations = CreateOperations(LayerId);
    }

    private YandexWebCalendarOperations CreateOperations(long? layerId)
    {
        return new YandexWebCalendarOperations(
            _maya,
            Options.Create(new YandexWebCalendarOptions { LayerId = layerId }),
            NullLogger<YandexWebCalendarOperations>.Instance);
    }

    private static CalDavEventInfo Event(DateTime start, params CalDavParticipant[] participants)
    {
        return new CalDavEventInfo("rehearsal-1@musicclub", "🎸 Репетиция", "desc", start, start.AddMinutes(80),
            false, "Кинотеатр", null, null, DateTime.UtcNow, participants);
    }

    [Test]
    public async Task GetCalendarsAsync_ReturnsConfiguredLayer()
    {
        var calendars = await _operations.GetCalendarsAsync();

        calendars.Count.ShouldBe(1);
        calendars[0].Url.ShouldBe(CalendarUrl);
        calendars[0].DisplayName!.ShouldContain("MusicClub");
    }

    [Test]
    public async Task GetCalendarsAsync_WithoutLayerId_ReturnsEmpty()
    {
        var calendars = await CreateOperations(null).GetCalendarsAsync();

        calendars.ShouldBeEmpty();
        await Should.ThrowAsync<NotSupportedException>(() => _operations.CreateCalendarAsync("MusicClub"));
    }

    [Test]
    public async Task CreateEventAsync_ConvertsUtcToMoscowAndSkipsNonEmailParticipants()
    {
        _maya.Handle("create-event", """{ "showEventId": 777 }""");
        var startUtc = new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);

        var created = await _operations.CreateEventAsync(CalendarUrl, Event(startUtc,
            new CalDavParticipant("musicclub", "Bot", CalDavParticipantRole.Required, CalDavParticipantStatus.NeedsAction),
            new CalDavParticipant("ivan@edu.centraluniversity.ru", "Иван", CalDavParticipantRole.Required, CalDavParticipantStatus.NeedsAction),
            new CalDavParticipant("petr@edu.centraluniversity.ru", "Пётр", CalDavParticipantRole.Optional, CalDavParticipantStatus.NeedsAction)));

        var call = _maya.Calls.Single();
        call.Model.ShouldBe("create-event");
        call.Params.GetProperty("layerId").GetInt64().ShouldBe(LayerId);
        call.Params.GetProperty("start").GetString().ShouldBe("2026-10-06T18:00:00");
        call.Params.GetProperty("end").GetString().ShouldBe("2026-10-06T19:20:00");
        call.Params.GetProperty("attendees").EnumerateArray().Select(a => a.GetString())
            .ShouldBe(["ivan@edu.centraluniversity.ru"]);
        call.Params.GetProperty("optionalAttendees").EnumerateArray().Select(a => a.GetString())
            .ShouldBe(["petr@edu.centraluniversity.ru"]);

        created.Url.ShouldBe("yandex-web://layer/36383467/event/777/2026-10-06");
        created.Start.ShouldBe(new DateTime(2026, 10, 6, 18, 0, 0));
    }

    [Test]
    public async Task GetEventAsync_FindsEventByIdInDayAndParsesTimes()
    {
        _maya.Handle("get-events", """
            { "events": [
                { "id": 1, "name": "Чужое", "startTs": "2026-10-06T10:00:00", "endTs": "2026-10-06T11:00:00" },
                { "id": 777, "name": "🎸 Репетиция", "startTs": "2026-10-06T15:00:00Z", "endTs": "2026-10-06T16:20:00Z",
                  "attendees": [ { "email": "ivan@edu.centraluniversity.ru", "decision": "yes" } ] }
            ] }
            """);

        var ev = await _operations.GetEventAsync("yandex-web://layer/36383467/event/777/2026-10-06");

        ev.ShouldNotBeNull();
        ev.Title.ShouldBe("🎸 Репетиция");
        ev.Start.ShouldBe(new DateTime(2026, 10, 6, 18, 0, 0));
        ev.End.ShouldBe(new DateTime(2026, 10, 6, 19, 20, 0));
        ev.Participants.Single().Status.ShouldBe(CalDavParticipantStatus.Accepted);

        var call = _maya.Calls.Single();
        call.Params.GetProperty("from").GetString().ShouldBe("2026-10-06");
        call.Params.GetProperty("to").GetString().ShouldBe("2026-10-07");
    }

    [Test]
    public async Task DeleteEventAsync_PassesInstanceStartTsAndLayer()
    {
        _maya.Handle("get-events", """
            { "events": [ { "id": 777, "name": "x", "startTs": "2026-10-06T18:00:00", "endTs": "2026-10-06T19:20:00",
                            "instanceStartTs": "2026-10-06T18:00:00", "sequence": 2 } ] }
            """);
        _maya.Handle("delete-event", "{}");

        await _operations.DeleteEventAsync("yandex-web://layer/36383467/event/777/2026-10-06");

        var delete = _maya.Calls.Single(c => c.Model == "delete-event").Params;
        delete.GetProperty("id").GetInt64().ShouldBe(777);
        delete.GetProperty("layerId").GetInt64().ShouldBe(LayerId);
        delete.GetProperty("instanceStartTs").GetString().ShouldBe("2026-10-06T18:00:00");
        delete.GetProperty("sequence").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task DeleteEventAsync_WhenEventMissing_DoesNothing()
    {
        _maya.Handle("get-events", """{ "events": [] }""");

        await _operations.DeleteEventAsync("yandex-web://layer/36383467/event/777/2026-10-06");

        _maya.Calls.ShouldNotContain(c => c.Model == "delete-event");
    }

    [Test]
    public async Task UpdateEventAsync_RecreatesEventAndReturnsNewUrl()
    {
        _maya.Handle("create-event", """{ "showEventId": 888 }""");
        _maya.Handle("get-events", """
            { "events": [ { "id": 777, "startTs": "2026-10-06T18:00:00", "endTs": "2026-10-06T19:20:00",
                            "instanceStartTs": "2026-10-06T18:00:00" } ] }
            """);
        _maya.Handle("delete-event", "{}");

        var updated = await _operations.UpdateEventAsync(Event(new DateTime(2026, 10, 7, 20, 0, 0)) with
        {
            Url = "yandex-web://layer/36383467/event/777/2026-10-06"
        });

        updated.Url.ShouldBe("yandex-web://layer/36383467/event/888/2026-10-07");
        _maya.Calls.Select(c => c.Model).ShouldBe(["create-event", "get-events", "delete-event"]);
    }

    [Test]
    public async Task IsUserBusyAsync_ByEmail_UsesEventsByLoginAndIgnoresDeclined()
    {
        _maya.Handle("get-events-by-login", """
            { "events": [
                { "start": "2026-10-06T15:00:00Z", "end": "2026-10-06T16:00:00Z", "decision": "no" },
                { "start": "2026-10-06T15:30:00Z", "end": "2026-10-06T16:00:00Z", "availability": "free" },
                { "start": "2026-10-06T10:00:00Z", "end": "2026-10-06T11:00:00Z" }
            ] }
            """);

        var busy = await _operations.IsUserBusyAsync("ivan@edu.centraluniversity.ru",
            new DateTime(2026, 10, 6, 18, 0, 0), new DateTime(2026, 10, 6, 19, 20, 0));

        busy.ShouldBeFalse();
        _maya.Calls.Single().Params.GetProperty("login").GetString().ShouldBe("ivan@edu.centraluniversity.ru");
    }

    [Test]
    public async Task IsUserBusyAsync_ByEmail_WhenOverlaps_ReturnsTrue()
    {
        _maya.Handle("get-events-by-login", """
            { "events": [ { "start": "2026-10-06T15:30:00Z", "end": "2026-10-06T16:30:00Z" } ] }
            """);

        var busy = await _operations.IsUserBusyAsync("ivan@edu.centraluniversity.ru",
            new DateTime(2026, 10, 6, 18, 0, 0), new DateTime(2026, 10, 6, 19, 20, 0));

        busy.ShouldBeTrue();
    }

    private sealed class FakeMayaClient : IYandexMayaClient
    {
        private readonly Dictionary<string, string> _responses = new();

        public List<(string Model, JsonElement Params)> Calls { get; } = [];

        public void Handle(string model, string json)
        {
            _responses[model] = json;
        }

        public Task<JsonElement> CallAsync(string model, object parameters, CancellationToken ct = default)
        {
            Calls.Add((model, JsonSerializer.SerializeToElement(parameters)));
            return Task.FromResult(JsonDocument.Parse(_responses[model]).RootElement.Clone());
        }
    }
}
