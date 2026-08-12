namespace CuMusicClub.Domain.Entities;

public class SongRole
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;

    public string RoleTitle { get; set; } = string.Empty;

    public SongRoleAssignment? Assignment { get; set; }
}

public class SongRoleAssignment
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Song Song { get; set; } = null!;
    public Guid RoleId { get; set; }
    public SongRole SongRole { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
}
