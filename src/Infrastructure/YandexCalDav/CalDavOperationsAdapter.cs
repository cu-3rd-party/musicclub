using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Infrastructure.YandexCalDav.Builders;
using CuMusicClub.Infrastructure.YandexCalDav.Models;
using Participant = CuMusicClub.Infrastructure.YandexCalDav.Models.Participant;
using ParticipantRole = CuMusicClub.Infrastructure.YandexCalDav.Models.ParticipantRole;
using ParticipantStatus = CuMusicClub.Infrastructure.YandexCalDav.Models.ParticipantStatus;

namespace CuMusicClub.Infrastructure.YandexCalDav;

/// <summary>
/// Адаптер, реализующий ICalDavOperations через YandexCalDavClient.
/// Позволяет Application слою зависеть от абстракции, а не от Infrastructure.
/// </summary>
public class CalDavOperationsAdapter : ICalDavOperations
{
    private readonly IYandexCalDavClient _client;

    public CalDavOperationsAdapter(IYandexCalDavClient client)
    {
        _client = client;
    }

    public async Task<List<CalDavCalendarInfo>> GetCalendarsAsync(CancellationToken ct = default)
    {
        var calendars = await _client.GetCalendarsAsync();
        return calendars.Select(c => new CalDavCalendarInfo(c.Url, c.DisplayName, c.Color, c.ETag)).ToList();
    }

    public async Task<CalDavCalendarInfo> CreateCalendarAsync(string displayName, string? color = null, CancellationToken ct = default)
    {
        var calendar = await _client.CreateCalendarAsync(displayName, color, ct);
        return new CalDavCalendarInfo(calendar.Url, calendar.DisplayName, calendar.Color, calendar.ETag);
    }

    public async Task<CalDavEventInfo> CreateEventAsync(string calendarUrl, CalDavEventInfo eventInfo, CancellationToken ct = default)
    {
        var builder = CalendarEventBuilder.Create()
            .WithUid(eventInfo.Uid)
            .WithTitle(eventInfo.Title)
            .WithDescription(eventInfo.Description)
            .WithTimeRange(eventInfo.Start, eventInfo.End)
            .WithLocation(eventInfo.Location);

        foreach (var participant in eventInfo.Participants)
        {
            var infraParticipant = ToInfrastructureParticipant(participant);
            builder.AddParticipant(infraParticipant);
        }

        var created = await _client.CreateEventAsync(calendarUrl, builder.Build());
        return MapToAppEventInfo(created);
    }

    public async Task<CalDavEventInfo> UpdateEventAsync(CalDavEventInfo eventInfo, CancellationToken ct = default)
    {
        var existing = await _client.GetEventAsync(eventInfo.Url ?? throw new InvalidOperationException("Event URL is required"));
        if (existing == null)
            throw new InvalidOperationException("Event not found");

        var builder = CalendarEventBuilder.Edit(existing)
            .WithTitle(eventInfo.Title)
            .WithDescription(eventInfo.Description)
            .WithTimeRange(eventInfo.Start, eventInfo.End)
            .WithLocation(eventInfo.Location);

        foreach (var participant in eventInfo.Participants)
        {
            var infraParticipant = ToInfrastructureParticipant(participant);
            builder.AddParticipant(infraParticipant);
        }

        var updated = await _client.UpdateEventAsync(builder.Build());
        return MapToAppEventInfo(updated);
    }

    public async Task DeleteEventAsync(string eventUrl, CancellationToken ct = default)
    {
        await _client.DeleteEventAsync(eventUrl);
    }

    public async Task<CalDavEventInfo?> GetEventAsync(string eventUrl, CancellationToken ct = default)
    {
        var existing = await _client.GetEventAsync(eventUrl);
        return existing == null ? null : MapToAppEventInfo(existing);
    }

    public async Task<List<CalDavEventInfo>> GetEventsAsync(string calendarUrl, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var events = await _client.GetEventsAsync(calendarUrl, startDate, endDate);
        return events.Select(MapToAppEventInfo).ToList();
    }

    public async Task<bool> IsUserBusyAsync(string calendarUrl, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var events = await _client.GetEventsAsync(calendarUrl, start, end.AddHours(2));
        return events.Any();
    }

    private static CalDavEventInfo MapToAppEventInfo(CalendarEventInfo info)
    {
        var participants = info.Participants.Select(ToApplicationParticipant).ToList();

        return new CalDavEventInfo(
            info.Uid,
            info.Title,
            info.Description,
            info.Start,
            info.End,
            info.IsAllDay,
            info.Location,
            info.ETag,
            info.Url,
            info.Created,
            participants);
    }

    private static Participant ToInfrastructureParticipant(CalDavParticipant appParticipant)
    {
        var role = appParticipant.Role switch
        {
            CalDavParticipantRole.Required => ParticipantRole.Required,
            CalDavParticipantRole.Chair => ParticipantRole.Chair,
            _ => ParticipantRole.Optional
        };

        var status = appParticipant.Status switch
        {
            CalDavParticipantStatus.Accepted => ParticipantStatus.Accepted,
            CalDavParticipantStatus.Declined => ParticipantStatus.Declined,
            CalDavParticipantStatus.Tentative => ParticipantStatus.Tentative,
            _ => ParticipantStatus.NeedsAction
        };

        return new Participant(appParticipant.Email, appParticipant.Name, role, status);
    }

    private static CalDavParticipant ToApplicationParticipant(Participant infraParticipant)
    {
        var role = infraParticipant.Role switch
        {
            ParticipantRole.Required => CalDavParticipantRole.Required,
            ParticipantRole.Chair => CalDavParticipantRole.Chair,
            _ => CalDavParticipantRole.Optional
        };

        var status = infraParticipant.Status switch
        {
            ParticipantStatus.Accepted => CalDavParticipantStatus.Accepted,
            ParticipantStatus.Declined => CalDavParticipantStatus.Declined,
            ParticipantStatus.Tentative => CalDavParticipantStatus.Tentative,
            _ => CalDavParticipantStatus.NeedsAction
        };

        return new CalDavParticipant(
            infraParticipant.Email,
            infraParticipant.Name,
            role,
            status);
    }
}
