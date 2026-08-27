namespace CuMusicClub.Domain.Entities;

public class SongTopicMember
{
    public Guid Id;
    public Guid UserId;
    public ApplicationUser User = null!;
    public long TopicId;
    public SongTopic Topic = null!;
}
