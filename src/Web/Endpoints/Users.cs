using CuMusicClub.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public class Users : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapIdentityApi<ApplicationUser>();

        group.MapPost("/logout", Logout).RequireAuthorization();
    }

    [EndpointSummary("Log out")]
    public static Ok Logout()
    {
        return TypedResults.Ok();
    }
}
