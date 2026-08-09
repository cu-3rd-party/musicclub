namespace CuMusicClub.Domain.Entities;

public class SongTopic
{
    public Guid SongId { get; set; }
    public Song Song { get; set; } = new();
    public long TopicId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
