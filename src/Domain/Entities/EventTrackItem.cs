namespace CuMusicClub.Domain.Entities;

public class EventTrackItem
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public int Position { get; set; }
    public Guid? SongId { get; set; }
    public string? CustomTitle { get; set; }
    public string? CustomArtist { get; set; }

    public Event? Event { get; set; }
    public Song? Song { get; set; }
}
