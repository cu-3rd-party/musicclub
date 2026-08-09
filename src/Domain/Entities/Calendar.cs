namespace CuMusicClub.Domain.Entities;

public class Calendar
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = new();
    public string CalendarUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CalendarAttachState
{
    public long TgUserId { get; set; }
    public string State { get; set; } = string.Empty;
    public Guid? PendingUserId { get; set; }
    public string? PendingEmail { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
