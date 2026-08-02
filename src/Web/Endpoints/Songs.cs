using System.Security.Claims;
using CuMusicClub.Application.Songs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public static class Songs
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/", List);
        group.MapGet("/{songId:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{songId:guid}", Update);
        group.MapDelete("/{songId:guid}", Delete);
        group.MapPost("/{songId:guid}/join", Join);
        group.MapPost("/{songId:guid}/leave", Leave);
    }

    [EndpointSummary("List songs")]
    private static async Task<Ok<ListSongsResultDto>> List(
        ISongService service, ClaimsPrincipal user, string? query, int? pageSize, string? pageToken, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(query, pageSize ?? 0, pageToken, user, cancellationToken);

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get a song by id")]
    private static async Task<Results<Ok<SongDetailsDto>, NotFound>> Get(
        ISongService service, ClaimsPrincipal user, Guid songId, CancellationToken cancellationToken)
    {
        var details = await service.GetAsync(songId, user, cancellationToken);

        return TypedResults.Ok(details);
    }

    [EndpointSummary("Create a song")]
    private static async Task<Results<Created<SongDetailsDto>, BadRequest>> Create(
        ISongService service, ClaimsPrincipal user, CreateSongRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.CreateAsync(request, user, cancellationToken);

        return TypedResults.Created($"/api/v1/songs/{details.Song.Id}", details);
    }

    [EndpointSummary("Update a song")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Update(
        ISongService service, ClaimsPrincipal user, Guid songId, UpdateSongRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.UpdateAsync(songId, request, user, cancellationToken);

        return TypedResults.Ok(details);
    }

    [EndpointSummary("Delete a song")]
    private static async Task<NoContent> Delete(
        ISongService service, ClaimsPrincipal user, Guid songId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(songId, user, cancellationToken);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Join a song role")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Join(
        ISongService service, ClaimsPrincipal user, Guid songId, RoleRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Role))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.JoinRoleAsync(songId, request.Role, user, cancellationToken);

        return TypedResults.Ok(details);
    }

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

public sealed record RoleRequest(string Role);
