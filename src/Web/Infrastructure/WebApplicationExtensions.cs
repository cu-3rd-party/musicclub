using CuMusicClub.Web.Endpoints;
using CuMusicClub.Web.Endpoints.v1.Auth;
using CuMusicClub.Web.Endpoints.v1.Calendar;
using CuMusicClub.Web.Endpoints.v1.Data;
using CuMusicClub.Web.Endpoints.v1.Ics;
using CuMusicClub.Web.Endpoints.v1.Songs;
using CuMusicClub.Web.Endpoints.v1.Users;
using CuMusicClub.Web.Endpoints.v1.YandexLogin;

namespace CuMusicClub.Web.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1");

        Auth.Map(v1
            .MapGroup("/auth")
            .WithTags("Auth"));

        Songs.Map(v1
            .MapGroup("/songs")
            .WithTags("Songs"));

        Data.Map(v1
            .MapGroup("/data")
            .WithTags("Data"));

        Users.Map(v1
            .MapGroup("/users")
            .WithTags("Users"));

        Calendar.Map(v1
            .MapGroup("/calendar")
            .WithTags("Calendar"));

        YandexLoginEndpoints.MapYandexLoginEndpoints(v1.MapGroup("/yandex-login"));

        IcsEndpoints.Map(app);

        return app;
    }
}
