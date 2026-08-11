using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Create a song")]
    private static async Task<Results<Created<SongDto>, BadRequest>> Create(ISongService service,
        ClaimsPrincipal user,
        CreateSongRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var song = await service.CreateAsync(request, user, cancellationToken);

        return TypedResults.Created($"/api/v1/songs/{song.Id}", song);
    }
}
