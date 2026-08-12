namespace CuMusicClub.Domain.Constants;

/// <summary>
/// Claim type that carries granular permissions (e.g. <c>"permission"</c> with value
/// <c>songs.edit_own</c>). Permissions are written as individual claims on the user
/// (<c>AspNetUserClaims</c>) and read back via <c>UserManager.GetClaimsAsync</c>.
/// </summary>
public static class PermissionClaimTypes
{
    public const string Permission = "permission";
}