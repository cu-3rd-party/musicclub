using Microsoft.IdentityModel.Tokens;

namespace CuMusicClub.Infrastructure.Options;

public class SecurityOptions
{
    public const string SectionName = "Security";
    public const string DefaultJwtKey = "dev-jwt-key";

    public required SymmetricSecurityKey SigningKey { get; set; }
}
