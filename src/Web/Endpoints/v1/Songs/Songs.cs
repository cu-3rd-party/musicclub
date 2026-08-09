namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
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
}

public sealed record RoleRequest(string Role);
