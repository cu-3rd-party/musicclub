using CuMusicClub.Web.Endpoints;

namespace CuMusicClub.Web.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/Users").WithTags("Users");
        Users.Map(users);

        return app;
    }
}
