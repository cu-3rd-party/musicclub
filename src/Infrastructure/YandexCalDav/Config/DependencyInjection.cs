using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.YandexCalDav.Config;

public static class DependencyInjection
{
    public static IServiceCollection AddYandexCalDav(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddTransient<IYandexCalDavClient, YandexCalDavClient>();

        return services;
    }
}
