using CuMusicClub.Application.WeatherForecasts.Queries.GetWeatherForecasts;

namespace CuMusicClub.Application.WeatherForecasts;

public class WeatherForecastService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public Task<IEnumerable<WeatherForecast>> GetForecastsAsync(CancellationToken cancellationToken = default)
    {
        var rng = new Random();
        var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = rng.Next(-20, 55),
            Summary = Summaries[rng.Next(Summaries.Length)]
        });

        return Task.FromResult(forecasts);
    }
}
