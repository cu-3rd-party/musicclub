using CuMusicClub.Web.Endpoints;

namespace CuMusicClub.Web.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/Users").WithTags("Users");
        Users.Map(users);

        var todoLists = app.MapGroup("/api/TodoLists").WithTags("TodoLists");
        TodoLists.Map(todoLists);

        var todoItems = app.MapGroup("/api/TodoItems").WithTags("TodoItems");
        TodoItems.Map(todoItems);

        var weather = app.MapGroup("/api/WeatherForecasts").WithTags("WeatherForecasts");
        WeatherForecasts.Map(weather);

        var shortenedUrls = app.MapGroup("/api/ShortenedUrls").WithTags("ShortenedUrls");
        ShortenedUrls.Map(shortenedUrls);
        app.MapGet("/url/{code}", ShortenedUrls.RedirectUrl);

        var bookmarkUrls = app.MapGroup("/api/Bookmarks").WithTags("Bookmarks");
        Bookmarks.Map(bookmarkUrls);

        return app;
    }
}
