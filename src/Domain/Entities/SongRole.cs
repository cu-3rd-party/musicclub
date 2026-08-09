namespace CuMusicClub.Domain.Entities;

public class SongRole
{
    public Guid SongId { get; set; }
    public Song Song { get; set; } = new();

    public string Role { get; set; } = string.Empty;

    public List<SongRoleAssignment> Assignments { get; set; } = [];
}

public class SongRoleAssignment
{
    public Guid Id { get; set; }
    public Guid SongId { get; set; }
    public Song Song { get; set; } = new();
    public string Role { get; set; } = string.Empty;
    public SongRole SongRole { get; set; } = new();

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = new();
    public DateTimeOffset JoinedAt { get; set; }
}
