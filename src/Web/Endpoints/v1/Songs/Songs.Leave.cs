using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Leave a song role")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Leave(
        ISongService service, ClaimsPrincipal user, Guid songId, RoleRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Role))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.LeaveRoleAsync(songId, request.Role, user, cancellationToken);

        return TypedResults.Ok(details);
    }
}
