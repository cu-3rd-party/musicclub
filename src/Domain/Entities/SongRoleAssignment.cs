namespace CuMusicClub.Domain.Entities;

public class SongRoleAssignment
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Song? Song { get; set; }
    public SongRole? SongRole { get; set; }
    public ApplicationUser? User { get; set; }
}
