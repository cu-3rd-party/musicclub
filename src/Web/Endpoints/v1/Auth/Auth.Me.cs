using System.Security.Claims;
using CuMusicClub.Application.Auth;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    [EndpointSummary("Get the current user's profile")]
    private static async Task<Results<Ok<UserProfileDto>, NotFound>> Me(ClaimsPrincipal claimsPrincipal,
        IApplicationDbContext db,
        IPermissionService permissionService,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(claimsPrincipal);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var profile = new UserProfileDto(Id: user.Id,
            DisplayName: user.DisplayName,
            Username: user.UserName!,
            AvatarUrl: user.AvatarUrl,
            Permissions: await permissionService.GetPermissionValuesAsync(user, cancellationToken),
            LastLoginAt: null,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt);

        return TypedResults.Ok(profile);
    }
}
