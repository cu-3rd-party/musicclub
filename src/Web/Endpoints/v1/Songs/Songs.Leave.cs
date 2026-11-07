using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Leave a song role")]
    private static async Task<Results<Ok<SongDto>, ProblemHttpResult>> Leave(ISongService service,
        IApplicationUserRepository users,
        ClaimsPrincipal claimsPrincipal,
        Guid roleId,
        RoleRequest? request,
        CancellationToken cancellationToken)
    {
        var target = await users.FindByIdAsync(claimsPrincipal.GetUserId());
        if (request != null) target = await users.FindByIdAsync(request.ActorUserId);
        if (target == null) return ApiProblems.BadRequest("Пользователь не найден.");

        var details = await service.LeaveRoleAsync(target, claimsPrincipal, roleId, cancellationToken);

        return TypedResults.Ok(details);
    }
}
