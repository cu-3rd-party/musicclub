namespace CuMusicClub.Domain.Entities;

public class UserPermission
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public bool EditOwnParticipation { get; set; }
    public bool EditAnyParticipation { get; set; }
    public bool EditOwnSongs { get; set; }
    public bool EditAnySongs { get; set; }
    public bool EditEvents { get; set; }
    public bool EditTracklists { get; set; }
    public bool EditFeaturedSongs { get; set; }
}
