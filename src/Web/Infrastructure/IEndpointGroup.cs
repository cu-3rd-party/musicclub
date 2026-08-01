namespace CuMusicClub.Web.Infrastructure;

public interface IEndpointGroup
{
    static abstract void Map(RouteGroupBuilder groupBuilder);
}
