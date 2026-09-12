using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Users;

public static partial class Users
{
    [EndpointSummary("Get the current user's privacy preferences")]
    private static async Task<Ok<UserPreferencesDto>> GetPreferences(ClaimsPrincipal claimsPrincipal,
        IApplicationUserRepository users,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        var prefs = await users.GetPreferencesAsync(userId, cancellationToken);

        return TypedResults.Ok(MapPreferences(prefs));
    }

    [EndpointSummary("Update the current user's privacy preferences")]
    private static async Task<Ok<UserPreferencesDto>> UpdatePreferences(ClaimsPrincipal claimsPrincipal,
        UpdateUserPreferencesRequest request,
        IApplicationUserRepository users,
        CancellationToken cancellationToken)
    {
        throw new ForbiddenAccessException();
        // var userId = claimsPrincipal.GetUserId();
        // await users.SetPreferencesAsync(userId,
        //     request.AllowAdding,
        //     request.AllowRemoving,
        //     cancellationToken);
        // await users.SaveChangesAsync(cancellationToken);
        //
        // var prefs = await users.GetPreferencesAsync(userId, cancellationToken);
        //
        // return TypedResults.Ok(MapPreferences(prefs));
    }

    private static UserPreferencesDto MapPreferences(UserPreferences? prefs)
    {
        return new UserPreferencesDto(prefs?.AllowAdding ?? true, prefs?.AllowRemoving ?? true);
    }
}
