using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Get a song by id")]
    private static async Task<Results<Ok<SongDetailsDto>, NotFound>> Get(
        ISongService service, ClaimsPrincipal user, Guid songId, CancellationToken cancellationToken)
    {
        var details = await service.GetAsync(songId, user, cancellationToken);

        return TypedResults.Ok(details);
    }
    
}
