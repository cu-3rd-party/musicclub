using CuMusicClub.Web.Endpoints;

namespace CuMusicClub.Web.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1");

        var auth = v1.MapGroup("/auth").WithTags("Auth");
        Auth.Map(auth);

        var songs = v1.MapGroup("/songs").WithTags("Songs");
        Songs.Map(songs);

        return app;
    }
}
