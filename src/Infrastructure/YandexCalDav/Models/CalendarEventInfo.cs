using CuMusicClub.Application.Common.Extensions;
using CuMusicClub.Infrastructure.YandexCalDav.Builders;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using NotFoundException = CuMusicClub.Infrastructure.YandexCalDav.Exceptions.NotFoundException;

namespace CuMusicClub.Infrastructure.YandexCalDav.Models;

public class CalendarEventInfo
{
    private readonly List<DateTime> _exceptionDates = [];
    private readonly List<Participant> _participants = [];
    private readonly List<CalendarEventInfo> _recurrenceExceptions = [];

    internal CalendarEventInfo(string uid, string title, string? description, DateTime start, DateTime end,
        bool isAllDay, string? location, string? eTag, string? url, DateTime created, RecurrencePattern? recurrenceRule,
        DateTime? recurrenceId, string? href = null)
    {
        Uid = uid;
        Title = title;
        Description = description;
        Start = start;
        End = end;
        IsAllDay = isAllDay;
        Location = location;
        ETag = eTag;
        Url = url;
        LastModified = Created;
        Created = created;
        RecurrenceRule = recurrenceRule;
        RecurrenceId = recurrenceId;
        Href = href;

        IsModified = false;
    }

    internal CalendarEventInfo(CalendarEvent calendarEvent, string href)
    {
        Uid = calendarEvent.Uid;
        Title = calendarEvent.Summary;
        Description = calendarEvent.Description;
        Start = calendarEvent.Start.Value;
        End = calendarEvent.End.Value;
        IsAllDay = calendarEvent.IsAllDay;
        Location = calendarEvent.Location;
        Url = calendarEvent.Url?.PathAndQuery;
        Created = calendarEvent.Created.Value;
        LastModified = calendarEvent.LastModified?.Value;
        Sequence = calendarEvent.Sequence;
        Href = href;

        if (calendarEvent.RecurrenceId != null) RecurrenceId = calendarEvent.RecurrenceId?.Value;

        if (calendarEvent.Attendees != null)
            foreach (var attendee in calendarEvent.Attendees)
            {
                var attendeeEmail = attendee.Value.ToString()
                    .Replace("mailto:", string.Empty, StringComparison.OrdinalIgnoreCase);
                var participant = new Participant(
                    attendeeEmail,
                    attendee.CommonName,
                    ParseParticipantRole(attendee.Role),
                    ParseParticipantStatus(attendee.ParticipationStatus),
                    ParseParticipantType(attendee.Type));
                _participants.Add(participant);
            }

        if (calendarEvent.RecurrenceRules?.Count > 0) RecurrenceRule = calendarEvent.RecurrenceRules[0];

        InitializeExceptionDates(calendarEvent);
    }

    public string Uid { get; }
    public string Title { get; }
    public string? Description { get; }
    public DateTime Start { get; }
    public DateTime End { get; }
    public bool IsAllDay { get; }
    public string? Location { get; }

    public IReadOnlyList<Participant> Participants
    {
        get { return _participants; }
    }

    public string? ETag { get; private set; }
    public string? Url { get; private set; }
    public DateTime? LastModified { get; private set; }
    public DateTime Created { get; }
    public int Sequence { get; private set; }

    // Recurrence properties
    public RecurrencePattern? RecurrenceRule { get; }

    public IReadOnlyList<DateTime> ExceptionDates
    {
        get { return _exceptionDates; }
    }

    public IReadOnlyList<CalendarEventInfo> RecurrenceExceptions
    {
        get { return _recurrenceExceptions; }
    }

    public DateTime? RecurrenceId { get; }

    public bool IsRecurrenceException
    {
        get { return RecurrenceId.HasValue; }
    }

    public bool IsRecurring
    {
        get { return RecurrenceRule != null; }
    }

    public string? Href { get; private set; }

    public bool IsModified { get; private set; }

    public CalendarEventInfo GetRecurrenceInstance(DateOnly date)
    {
        if (!IsRecurring) throw new ArgumentException("Event must be recurring to modify an instance");

        var existingException = GetExistingException(date);
        if (existingException != null) return existingException;

        var recurrentEvent = Calendar
            .Load(ToIcal())
            .Events
            .SingleOrDefault(e => e.RecurrenceRules.Any() && e.RecurrenceId == null);
        if (recurrentEvent == null) throw new NotFoundException($"Event {Uid} has no instance on {date:d}");

        var dateOccurence = recurrentEvent.GetOccurrences(date.ToDateTime()).SingleOrDefault();
        if (dateOccurence == null) throw new NotFoundException($"Event {Uid} has no instance on {date:d}");

        return CreateInstanceForDate(dateOccurence.Period.StartTime.Value);
    }

    public void ModifyRecurrenceInstance(DateOnly instanceDate, Action<CalendarEventBuilder> modifications)
    {
        if (!IsRecurring) throw new ArgumentException("Event must be recurring to modify an instance");

        var instance = GetRecurrenceInstance(instanceDate);

        if (instance == null) throw new ArgumentException($"No instance found for date {instanceDate:yyyy-MM-dd}");

        var builder = CalendarEventBuilder.Edit(instance);
        modifications(builder);
        var modifiedInstance = builder.Build();

        var existingException = GetExistingException(instanceDate);
        if (existingException != null) _recurrenceExceptions.Remove(existingException);

        _recurrenceExceptions.Add(modifiedInstance);

        IsModified = true;
        Sequence++;
        LastModified = DateTime.UtcNow;
    }

    public void CancelRecurrenceInstance(DateOnly date)
    {
        if (!IsRecurring) throw new ArgumentException("Event must be recurring to cancel an instance");

        var instance = GetRecurrenceInstance(date);

        if (instance.RecurrenceId.HasValue && !ExceptionDates.Contains(instance.RecurrenceId.Value))
        {
            _exceptionDates.Add(instance.RecurrenceId.Value);
            IsModified = true;
            Sequence++;
            LastModified = DateTime.UtcNow;
        }
    }

    public void RestoreRecurrenceInstance(DateOnly date)
    {
        if (!IsRecurring) throw new ArgumentException("Event must be recurring to cancel an instance");

        if (ExceptionDates.All(d => d.Date != date.ToDateTime()))
            throw new NotFoundException($"No cancelled instance found for date: {date:d}");

        _exceptionDates.Remove(_exceptionDates.Single(d => d.Date == date.ToDateTime()));
        IsModified = true;
        Sequence++;
        LastModified = DateTime.UtcNow;
    }

    public static CalendarEventInfo? FromIcal(string iCalData, string href)
    {
        var calendar = Calendar.Load(iCalData);
        if (calendar.Events.Count == 0) return null;

        CalendarEvent? mainCalendarEvent = null;
        var exceptionEvents = new List<CalendarEvent>();

        foreach (var evt in calendar.Events)
            if (evt.RecurrenceId != null)
                exceptionEvents.Add(evt);
            else
                mainCalendarEvent = evt;

        if (mainCalendarEvent == null) return new CalendarEventInfo(calendar.Events[0], href);

        var mainEventInfo = new CalendarEventInfo(mainCalendarEvent, href);

        if (exceptionEvents.Count > 0)
        {
            var exceptions = exceptionEvents.Select(e => new CalendarEventInfo(e, href)).ToArray();
            mainEventInfo.SetRecurrenceExceptions(exceptions);
        }

        return mainEventInfo;
    }

    public string ToIcal(Organizer? organizer = null)
    {
        var calendar = new Calendar();

        var calendarEvent = CreateCalendarEventFromInfo(this, organizer);
        calendar.Events.Add(calendarEvent);

        foreach (var exception in _recurrenceExceptions)
        {
            var exceptionEvent = CreateCalendarEventFromInfo(exception, organizer);
            exceptionEvent.RecurrenceId = new CalDateTime(exception.RecurrenceId!.Value);
            calendar.Events.Add(exceptionEvent);
        }

        return new CalendarSerializer().SerializeToString(calendar);
    }

    internal void SetModified(bool modified, int? fromSequence = null)
    {
        IsModified = modified;

        if (modified)
        {
            if (fromSequence.HasValue) Sequence = fromSequence.Value;

            Sequence++;
            LastModified = DateTime.UtcNow;
        }
    }

    internal void SetEtag(string? etag)
    {
        ETag = etag;
    }

    internal void SetHref(string href)
    {
        Href = href;
    }

    internal void SetParticipants(IEnumerable<Participant> participants)
    {
        _participants.Clear();
        _participants.AddRange(participants);
    }

    internal void SetExceptionDates(IEnumerable<DateTime> exceptionDates)
    {
        _exceptionDates.Clear();
        _exceptionDates.AddRange(exceptionDates);
    }

    internal void SetRecurrenceExceptions(IEnumerable<CalendarEventInfo> recurrenceExceptions)
    {
        _recurrenceExceptions.Clear();
        _recurrenceExceptions.AddRange(recurrenceExceptions);
    }

    private void InitializeExceptionDates(CalendarEvent calendarEvent)
    {
        if (calendarEvent.ExceptionDates != null)
        {
            var exceptionDates = calendarEvent.ExceptionDates
                .SelectMany(p => p.ToArray())
                .Select(p => p?.StartTime)
                .Where(p => p != null);
            foreach (var period in exceptionDates) _exceptionDates.Add(period!.Value);
        }
    }

    private CalendarEventInfo? GetExistingException(DateOnly instanceDate)
    {
        return _recurrenceExceptions.Find(i => i.RecurrenceId?.Date == instanceDate.ToDateTime());
    }

    private static CalendarEvent CreateCalendarEventFromInfo(CalendarEventInfo eventInfo, Organizer? organizer = null)
    {
        var calendarEvent = new CalendarEvent
        {
            Uid = eventInfo.Uid,
            Summary = eventInfo.Title,
            Description = eventInfo.Description,
            Start = new CalDateTime(eventInfo.Start.FromMskToUtc()),
            End = new CalDateTime(eventInfo.End.FromMskToUtc()),
            IsAllDay = eventInfo.IsAllDay,
            Location = eventInfo.Location,
            Created = new CalDateTime(eventInfo.Created),
            Sequence = eventInfo.Sequence
        };

        if (eventInfo.LastModified.HasValue) calendarEvent.LastModified = new CalDateTime(eventInfo.LastModified.Value);

        foreach (var participant in eventInfo.Participants)
        {
            var attendee = new Attendee(new Uri($"mailto:{participant.Email}"))
            {
                CommonName = participant.Name,
                Role = ConvertParticipantRole(participant.Role),
                ParticipationStatus = ConvertParticipantStatus(participant.Status),
                Type = ConvertParticipantType(participant.Type)
            };
            calendarEvent.Attendees.Add(attendee);
        }

        if (eventInfo is { RecurrenceRule: not null, IsRecurrenceException: false })
            calendarEvent.RecurrenceRules.Add(eventInfo.RecurrenceRule);

        if (!eventInfo.IsRecurrenceException)
            foreach (var exceptionDate in eventInfo.ExceptionDates)
                calendarEvent.ExceptionDates.Add(new PeriodList { new CalDateTime(exceptionDate) });

        calendarEvent.Organizer = organizer;

        return calendarEvent;
    }

    private CalendarEventInfo CreateInstanceForDate(DateTime startTime)
    {
        var duration = End - Start;

        var instance = new CalendarEventInfo(
            Uid,
            Title,
            Description,
            startTime,
            startTime.Add(duration),
            IsAllDay,
            Location,
            null,
            null,
            DateTime.UtcNow,
            null,
            startTime);

        instance.SetParticipants(Participants);

        return instance;
    }

    private static ParticipantRole ParseParticipantRole(string role)
    {
        return role.ToUpperInvariant() switch
        {
            "CHAIR" => ParticipantRole.Chair,
            "REQ-PARTICIPANT" => ParticipantRole.Required,
            _ => ParticipantRole.Optional
        };
    }

    private static ParticipantStatus ParseParticipantStatus(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "ACCEPTED" => ParticipantStatus.Accepted,
            "DECLINED" => ParticipantStatus.Declined,
            "TENTATIVE" => ParticipantStatus.Tentative,
            "DELEGATED" => ParticipantStatus.Delegated,
            "COMPLETED" => ParticipantStatus.Completed,
            "IN-PROCESS" => ParticipantStatus.InProcess,
            _ => ParticipantStatus.NeedsAction
        };
    }

    private static ParticipantType ParseParticipantType(string? type)
    {
        return type?.ToUpperInvariant() switch
        {
            "UNKNOWN" => ParticipantType.Unknown,
            "INDIVIDUAL" => ParticipantType.Individual,
            "ROOM" => ParticipantType.Room,
            _ => ParticipantType.Unknown
        };
    }

    private static string ConvertParticipantRole(ParticipantRole role)
    {
        return role switch
        {
            ParticipantRole.Chair => "CHAIR",
            ParticipantRole.Required => "REQ-PARTICIPANT",
            _ => "OPT-PARTICIPANT"
        };
    }

    private static string ConvertParticipantStatus(ParticipantStatus status)
    {
        return status switch
        {
            ParticipantStatus.Accepted => "ACCEPTED",
            ParticipantStatus.Declined => "DECLINED",
            ParticipantStatus.Tentative => "TENTATIVE",
            ParticipantStatus.Delegated => "DELEGATED",
            ParticipantStatus.Completed => "COMPLETED",
            ParticipantStatus.InProcess => "IN-PROCESS",
            _ => "NEEDS-ACTION"
        };
    }

    private static string ConvertParticipantType(ParticipantType type)
    {
        return type switch
        {
            ParticipantType.Unknown => "UNKNOWN",
            ParticipantType.Individual => "INDIVIDUAL",
            ParticipantType.Room => "ROOM",
            _ => "UNKNOWN"
        };
    }
}
