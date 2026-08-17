namespace CuMusicClub.Web.Endpoints.v1.Data;

public static partial class Data
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{dataId:guid}", Get);
    }
}
