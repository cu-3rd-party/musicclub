using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.Entities;

public class Song
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SongLinkType LinkKind { get; set; }
    public string LinkUrl { get; set; } = string.Empty;
    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsFeatured { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<SongRole> Roles { get; set; } = [];
    public List<SongRoleAssignment> Assignments { get; set; } = [];
}
