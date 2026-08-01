using CuMusicClub.Application.WeatherForecasts;
using CuMusicClub.Application.WeatherForecasts.Queries.GetWeatherForecasts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public class WeatherForecasts : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/", GetWeatherForecasts);
    }

    [EndpointSummary("Get Weather Forecasts")]
    public static async Task<Ok<IEnumerable<WeatherForecast>>> GetWeatherForecasts(WeatherForecastService service)
    {
        var forecasts = await service.GetForecastsAsync();
        return TypedResults.Ok(forecasts);
    }
}
