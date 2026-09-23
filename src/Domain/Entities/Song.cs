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
    public Guid? ThumbnailDataEntryId { get; set; }
    public DataEntry? ThumbnailDataEntry { get; set; }
    public bool IsFeatured { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<SongRole> Roles { get; set; } = [];
    public List<SongRoleAssignment> Assignments { get; set; } = [];
    public SongTopic? SongTopic { get; set; }

    /// <summary>
    /// Returns true if all roles in the song are filled (have assignments).
    /// A song with no roles is considered not full.
    /// </summary>
    public bool IsFull => Roles.Count > 0 && Roles.All(r => r.Assignment != null);
}
