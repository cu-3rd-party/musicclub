using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Delete a song")]
    private static async Task<NoContent> Delete(
        ISongService service, ClaimsPrincipal user, Guid songId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(songId, user, cancellationToken);

        return TypedResults.NoContent();
    }
}
