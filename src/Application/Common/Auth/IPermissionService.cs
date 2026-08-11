using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Common.Auth;

/// <summary>
/// Central place for reading/writing user permissions.
/// Permissions always live as individual <c>permission</c> claims on the user
/// (<c>AspNetUserClaims</c>); roles are only sugar bundles that materialize those claims.
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionValuesAsync(ApplicationUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the given permission values as individual <c>permission</c> claims on the user
    /// (idempotent — existing claims are left untouched).
    /// </summary>
    Task GrantPermissionsAsync(ApplicationUser user, IEnumerable<string> permissions, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a role to the user. The role itself is pure sugar: membership is recorded and
    /// the role's permission bundle is materialized as individual claims on the user.
    /// </summary>
    Task GrantRoleAsync(ApplicationUser user, string role, CancellationToken cancellationToken);

    /// <summary>
    /// Grants the default permission bundle to the user.
    /// </summary>
    Task GrantDefaultAsync(ApplicationUser user, CancellationToken cancellationToken);
}
