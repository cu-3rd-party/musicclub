namespace CuMusicClub.Domain.Entities;

public class TgAuthUser
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long? TgUserId { get; set; }
    public bool Success { get; set; }

    public AppUser? User { get; set; }
}
