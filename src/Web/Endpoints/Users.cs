using CuMusicClub.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public static class Users
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapIdentityApi<ApplicationUser>();

        group.MapPost("/logout", Logout).RequireAuthorization();
    }

    [EndpointSummary("Log out")]
    private static Ok Logout()
    {
        return TypedResults.Ok();
    }
}
