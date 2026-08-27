namespace CuMusicClub.Domain.Entities;

public class SongTopic
{
    public Guid SongId { get; set; }
    public required Song Song { get; set; }
    public string Title { get; set; } = "";
    public long TopicId { get; set; }
    public long? ChatId { get; set; } // опционально, в каком чате топик был создан
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public IEnumerable<SongTopicMember> TopicMembers { get; set; } = [];
}
