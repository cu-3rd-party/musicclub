using Microsoft.AspNetCore.Identity;

namespace CuMusicClub.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public long? TgUserId { get; set; }
    public bool IsChatMember { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
