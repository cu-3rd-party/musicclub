namespace CuMusicClub.Domain.Entities;

public class SongRole
{
    public Guid SongId { get; set; }
    public string Role { get; set; } = string.Empty;

    public Song? Song { get; set; }
    public List<SongRoleAssignment> Assignments { get; set; } = [];
}
