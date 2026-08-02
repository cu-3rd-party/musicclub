namespace CuMusicClub.Domain.Entities;

public class EventParticipant
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? TrackItemId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }

    public Event? Event { get; set; }
    public EventTrackItem? TrackItem { get; set; }
    public ApplicationUser? User { get; set; }
}
