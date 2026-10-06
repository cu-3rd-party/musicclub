using CuMusicClub.Infrastructure.YandexCalDav.Models;
using Ical.Net;
using Ical.Net.DataTypes;

namespace CuMusicClub.Infrastructure.YandexCalDav.Builders;

public sealed class CalendarEventBuilder
{
    private readonly CalendarEventInfo? _prototype;
    private string? _description;
    private DateTime? _end;
    private List<DateTime>? _exceptionDates;
    private bool? _isAllDay;
    private string? _location;
    private List<Participant>? _participants;
    private RecurrencePattern? _recurrenceRule;
    private DateTime? _start;
    private string? _title;
    private string? _uid;

    private CalendarEventBuilder()
    {
    }

    private CalendarEventBuilder(CalendarEventInfo existingEvent)
    {
        _prototype = existingEvent ?? throw new ArgumentNullException(nameof(existingEvent));
        _exceptionDates = _prototype.ExceptionDates.ToList();
        _participants = _prototype.Participants.ToList();
    }

    public CalendarEventBuilder WithUid(string uid)
    {
        _uid = uid;
        return this;
    }

    public CalendarEventBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public CalendarEventBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CalendarEventBuilder WithStartTime(DateTime start)
    {
        _start = start;
        return this;
    }

    public CalendarEventBuilder WithEndTime(DateTime end)
    {
        _end = end;
        return this;
    }

    public CalendarEventBuilder WithTimeRange(DateTime start, DateTime end)
    {
        _start = start;
        _end = end;
        return this;
    }

    public CalendarEventBuilder AsAllDay(DateTime date, bool isAllDay = true)
    {
        _isAllDay = isAllDay;
        if (isAllDay)
        {
            _start = date;
            _end = date;
        }

        return this;
    }

    public CalendarEventBuilder WithLocation(string location)
    {
        _location = location;
        return this;
    }

    public CalendarEventBuilder AddRequiredParticipant(string email, string? name = null)
    {
        var toAdd = Participant.CreateRequired(email, name);
        return AddParticipant(toAdd);
    }

    public CalendarEventBuilder AddOptionalParticipant(string email, string? name = null)
    {
        var toAdd = Participant.CreateOptional(email, name);
        return AddParticipant(toAdd);
    }

    public CalendarEventBuilder AddParticipant(Participant participant)
    {
        _participants ??= [];
        if (!_participants.Contains(participant)) _participants.Add(participant);

        return this;
    }

    public CalendarEventBuilder WithParticipants(IEnumerable<Participant> participants)
    {
        _participants ??= [];
        _participants.Clear();
        _participants.AddRange(participants);
        return this;
    }

    public CalendarEventBuilder WithRecurrence(RecurrencePattern recurrenceRule)
    {
        _recurrenceRule = recurrenceRule;
        return this;
    }

    public CalendarEventBuilder WithDailyRecurrence(int interval = 1, DateTime? until = null, int? count = null)
    {
        var pattern = new RecurrencePattern(FrequencyType.Daily, interval);
        if (until.HasValue) pattern.Until = until.Value;

        if (count.HasValue) pattern.Count = count.Value;

        _recurrenceRule = pattern;
        return this;
    }

    public CalendarEventBuilder WithWeeklyRecurrence(DayOfWeek dayOfWeek, int interval = 1, DateTime? until = null,
        int? count = null)
    {
        var pattern = new RecurrencePattern(FrequencyType.Weekly, interval);
        if (until.HasValue) pattern.Until = until.Value;

        if (count.HasValue) pattern.Count = count.Value;

        pattern.ByDay.Add(new WeekDay(dayOfWeek));

        _recurrenceRule = pattern;
        return this;
    }

    public CalendarEventBuilder WithMonthlyRecurrence(int interval = 1, DateTime? until = null, int? count = null)
    {
        var pattern = new RecurrencePattern(FrequencyType.Monthly, interval);
        if (until.HasValue) pattern.Until = until.Value;

        if (count.HasValue) pattern.Count = count.Value;

        _recurrenceRule = pattern;
        return this;
    }

    public CalendarEventBuilder AddExceptionDate(DateTime exceptionDate)
    {
        _exceptionDates ??= _prototype?.ExceptionDates.ToList() ?? [];
        _exceptionDates.Add(exceptionDate);
        return this;
    }

#pragma warning disable CA1502, S1541
    public CalendarEventInfo Build()
    {
        _title ??= _prototype?.Title;
        _start ??= _prototype?.Start;
        _end ??= _prototype?.End;

        if (string.IsNullOrWhiteSpace(_title)) throw new InvalidOperationException("Event title is required");

        if (_start == null) throw new InvalidOperationException("Event start time is required");

        _end ??= _start.Value.AddHours(1);

        if (_end <= _start) throw new InvalidOperationException("Event end time must be after start time");

        var now = DateTime.UtcNow;

        var result = new CalendarEventInfo(
            _prototype != null ? _prototype.Uid : _uid ?? Guid.NewGuid().ToString(),
            _title,
            _description ?? _prototype?.Description,
            _start.Value,
            _end.Value,
            _isAllDay ?? _prototype?.IsAllDay ?? false,
            _location ?? _prototype?.Location,
            _prototype?.ETag,
            _prototype?.Url,
            _prototype?.Created ?? now,
            _recurrenceRule ?? _prototype?.RecurrenceRule,
            _prototype?.RecurrenceId,
            _prototype?.Href);
        if (_participants != null) result.SetParticipants(_participants);

        if (_exceptionDates != null) result.SetExceptionDates(_exceptionDates);

        if (_prototype?.RecurrenceExceptions is { Count: > 0 })
            result.SetRecurrenceExceptions(_prototype.RecurrenceExceptions);

        if (_prototype != null) result.SetModified(true, _prototype.Sequence);

        return result;
    }
#pragma warning restore CA1502, S1541

    public static CalendarEventBuilder Create()
    {
        return new CalendarEventBuilder();
    }

    public static CalendarEventBuilder Edit(CalendarEventInfo existingEvent)
    {
        return new CalendarEventBuilder(existingEvent);
    }
}
