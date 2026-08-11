namespace CuMusicClub.Domain.Entities;

public class SongRole
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Song? Song { get; set; }

    public string RoleTitle { get; set; } = string.Empty;

    public SongRoleAssignment? Assignment { get; set; }
}

public class SongRoleAssignment
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Song? Song { get; set; }
    public Guid RoleId { get; set; }
    public SongRole? SongRole { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
