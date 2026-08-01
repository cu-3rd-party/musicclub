using System.Reflection;
using CuMusicClub.Application.WeatherForecasts;
using FluentValidation;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        builder.Services.AddScoped<WeatherForecastService>();
    }
}
