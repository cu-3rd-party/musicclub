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
