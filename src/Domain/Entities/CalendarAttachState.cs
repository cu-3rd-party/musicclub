namespace CuMusicClub.Domain.Entities;

public class CalendarAttachState
{
    public long TgUserId { get; set; }
    public short State { get; set; }
    public Guid? PendingUserId { get; set; }
    public string? PendingEmail { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
