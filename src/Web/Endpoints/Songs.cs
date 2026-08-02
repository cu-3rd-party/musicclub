using CuMusicClub.Application.Songs;
using CuMusicClub.Web.Infrastructure;
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
        ISongService service, string? query, int? pageSize, string? pageToken, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(query, pageSize ?? 0, pageToken, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get a song by id")]
    private static async Task<Results<Ok<SongDetailsDto>, NotFound>> Get(
        ISongService service, Guid songId, HttpContext context, CancellationToken cancellationToken)
    {
        var details = await service.GetAsync(songId, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Ok(details);
    }

    [EndpointSummary("Create a song")]
    private static async Task<Results<Created<SongDetailsDto>, BadRequest>> Create(
        ISongService service, CreateSongRequest? request, HttpContext context, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.CreateAsync(request, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Created($"/api/v1/songs/{details.Song.Id}", details);
    }

    [EndpointSummary("Update a song")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Update(
        ISongService service, Guid songId, UpdateSongRequest? request, HttpContext context, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Artist))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.UpdateAsync(songId, request, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Ok(details);
    }

    [EndpointSummary("Delete a song")]
    private static async Task<NoContent> Delete(
        ISongService service, Guid songId, HttpContext context, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(songId, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.NoContent();
    }

    [EndpointSummary("Join a song role")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Join(
        ISongService service, Guid songId, RoleRequest? request, HttpContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Role))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.JoinRoleAsync(songId, request.Role, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Ok(details);
    }

    [EndpointSummary("Leave a song role")]
    private static async Task<Results<Ok<SongDetailsDto>, BadRequest>> Leave(
        ISongService service, Guid songId, RoleRequest? request, HttpContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Role))
        {
            return TypedResults.BadRequest();
        }

        var details = await service.LeaveRoleAsync(songId, request.Role, context.User.GetAppUserId(), cancellationToken);

        return TypedResults.Ok(details);
    }
}

public sealed record RoleRequest(string Role);
