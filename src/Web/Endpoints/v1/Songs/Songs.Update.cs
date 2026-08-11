using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Update a song")]
    private static async Task<Results<Ok<SongDto>, BadRequest>> Update(
        ISongService service, ClaimsPrincipal user, Guid songId, UpdateSongRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.UpdateAsync(songId, request, user, cancellationToken);

        return TypedResults.Ok(details);
    }
}
