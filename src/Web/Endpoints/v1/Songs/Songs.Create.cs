using System.Security.Claims;
using CuMusicClub.Application.Services.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Create a song")]
    private static async Task<Results<Created<SongDto>, ProblemHttpResult>> Create(ISongService service,
        ClaimsPrincipal user,
        CreateSongRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return ApiProblems.BadRequest("Не переданы данные песни.");
        if (string.IsNullOrWhiteSpace(request.Title)) return ApiProblems.Validation("title", "Укажите название песни.");
        if (string.IsNullOrWhiteSpace(request.Artist)) return ApiProblems.Validation("artist", "Укажите исполнителя.");

        var song = await service.CreateAsync(request, user, cancellationToken);

        return TypedResults.Created($"/api/v1/songs/{song.Id}", song);
    }
}
