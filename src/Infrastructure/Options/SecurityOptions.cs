using Microsoft.IdentityModel.Tokens;

namespace CuMusicClub.Infrastructure.Options;

public class SecurityOptions
{
    public const string SectionName = "Security";
    public const string DefaultJwtKey = "dev-jwt-key-very-long-key-longer-than-128-bits-is-required";

    /// <summary>
    /// В конфиге должно быть Security:Secret
    /// </summary>
    public required SymmetricSecurityKey SigningKey { get; set; }
}
