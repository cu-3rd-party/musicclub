using Microsoft.AspNetCore.Identity;

namespace CuMusicClub.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public long? TgUserId { get; set; }
}
