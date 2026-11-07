using System.Security.Claims;
using CuMusicClub.Application.Services.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Update a song")]
    private static async Task<Results<Ok<SongDto>, ProblemHttpResult>> Update(ISongService service,
        ClaimsPrincipal user,
        Guid songId,
        UpdateSongRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return ApiProblems.BadRequest("Не переданы данные песни.");
        if (string.IsNullOrWhiteSpace(request.Title)) return ApiProblems.Validation("title", "Укажите название песни.");
        if (string.IsNullOrWhiteSpace(request.Artist)) return ApiProblems.Validation("artist", "Укажите исполнителя.");

        var details = await service.UpdateAsync(songId, request, user, cancellationToken);

        return TypedResults.Ok(details);
    }
}
