using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CuMusicClub.Infrastructure.Services;

public class PermissionService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager
) : IPermissionService
{
    public async Task<IReadOnlyList<string>> GetPermissionValuesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var claims = await userManager.GetClaimsAsync(user);
        return claims
            .Where(c => c.Type == PermissionClaimTypes.Permission)
            .Select(c => c.Value)
            .ToList();
    }

    public async Task GrantPermissionsAsync(ApplicationUser user, IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        var existing = await userManager.GetClaimsAsync(user);
        var existingSet = existing
            .Where(c => c.Type == PermissionClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            if (existingSet.Contains(permission))
            {
                continue;
            }

            await userManager.AddClaimAsync(user, new Claim(PermissionClaimTypes.Permission, permission));
        }
    }

    public async Task GrantRoleAsync(ApplicationUser user, string role, CancellationToken cancellationToken)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var createResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{role}': " +
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addResult = await userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign role '{role}' to user '{user.Id}': " +
                    string.Join(", ", addResult.Errors.Select(e => e.Description)));
            }
        }

        if (Permissions.ByRole.TryGetValue(role, out var bundle))
        {
            await GrantPermissionsAsync(user, bundle, cancellationToken);
        }
    }

    public Task GrantDefaultAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return GrantPermissionsAsync(user, Permissions.Default, cancellationToken);
    }
}
