using CuMusicClub.Application.Services.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Get a song by id")]
    private static async Task<Results<Ok<SongDto>, NotFound>> Get(ISongService service,
        Guid songId,
        CancellationToken cancellationToken)
    {
        var details = await service.GetAsync(songId, cancellationToken);

        return TypedResults.Ok(details);
    }
}
