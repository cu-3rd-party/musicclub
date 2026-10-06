using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Infrastructure.YandexCalDav.Config;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Веб-API: пишем, только если выбран календарь (LayerId) и есть чем авторизоваться.
/// </summary>
public sealed class YandexWebCalendarIntegration(
    IOptions<YandexWebCalendarOptions> options,
    IYandexWebSession session) : ICalendarIntegration
{
    public bool IsSyncEnabled
    {
        get
        {
            var value = options.Value;
            return value.SyncEnabled && value.LayerId.HasValue && session.IsConfigured;
        }
    }
}

/// <summary>
///     CalDAV: пишем, только если заданы логин и пароль сервисного аккаунта.
/// </summary>
public sealed class YandexCalDavIntegration(
    IOptions<YandexWebCalendarOptions> options,
    IOptions<YandexCaldavConfig> calDavConfig) : ICalendarIntegration
{
    public bool IsSyncEnabled
    {
        get
        {
            var config = calDavConfig.Value;
            return options.Value.SyncEnabled
                   && !string.IsNullOrWhiteSpace(config.User)
                   && !string.IsNullOrWhiteSpace(config.Password);
        }
    }
}
