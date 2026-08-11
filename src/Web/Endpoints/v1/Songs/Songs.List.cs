using System.Security.Claims;
using CuMusicClub.Application.Song;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("List songs")]
    private static async Task<Ok<ListSongsResultDto>> List(ISongService service,
        ClaimsPrincipal user,
        string? query,
        int? pageSize,
        string? pageToken,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(query, pageSize ?? 0, pageToken, user, cancellationToken);

        return TypedResults.Ok(result);
    }
}
