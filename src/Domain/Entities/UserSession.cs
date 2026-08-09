namespace CuMusicClub.Domain.Entities;

public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; } = Guid.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? ScreenResolution { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }

    public ApplicationUser? User { get; set; }
}
