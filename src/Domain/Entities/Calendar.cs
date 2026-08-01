namespace CuMusicClub.Domain.Entities;

public class Calendar
{
    public Guid UserId { get; set; }
    public string CalendarUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AppUser? User { get; set; }
}
