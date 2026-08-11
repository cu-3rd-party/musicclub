using System.Security.Claims;
using CuMusicClub.Application.Song;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Leave a song role")]
    private static async Task<Results<Ok<SongDto>, BadRequest<string>>> Leave(
        ISongService service,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        RoleRequest? request,
        CancellationToken cancellationToken)
    {
        var target = await userManager.GetUserAsync(claimsPrincipal);
        if (request != null)
            target = await userManager.FindByIdAsync(request.ActorUserId.ToString());
        if (target == null)
            return TypedResults.BadRequest("no target user found");

        var details = await service.LeaveRoleAsync(target, claimsPrincipal, roleId, cancellationToken);

        return TypedResults.Ok(details);
    }
}
