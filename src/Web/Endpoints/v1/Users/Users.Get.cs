using System.Security.Claims;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Domain.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Users;

public static partial class Users
{
    [EndpointSummary("Get user info by id")]
    public static async Task<Results<Ok<UserProfileDto>, NotFound>> Get(IApplicationUserRepository users,
        IPermissionService permissionService,
        Guid userId,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId);

        if (user == null)
            return TypedResults.NotFound();

        var permissions = await users.GetPermissionsAsync(userId, cancellationToken);

        var profile = new UserProfileDto(
            user.Id,
            user.DisplayName,
            user.UserName ?? "",
            user.AvatarUrl,
            permissions,
            null, // LastLoginAt is not tracked yet
            user.CreatedAt,
            user.UpdatedAt);

        return TypedResults.Ok(profile);
    }
}
