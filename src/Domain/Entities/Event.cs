namespace CuMusicClub.Domain.Entities;

public class Event
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? StartAt { get; set; }
    public string? Location { get; set; }
    public bool NotifyDayBefore { get; set; }
    public bool NotifyHourBefore { get; set; }
    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<EventTrackItem> TrackItems { get; set; } = [];
    public List<EventParticipant> Participants { get; set; } = [];
}

public class EventTrackItem
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = new();
    public int Position { get; set; }
    public Guid? SongId { get; set; }
    public Song? Song { get; set; }
    public string? CustomTitle { get; set; }
    public string? CustomArtist { get; set; }
}

public class EventParticipant
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = new();
    public Guid? TrackItemId { get; set; }
    public EventTrackItem? TrackItem { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = new();
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
}
