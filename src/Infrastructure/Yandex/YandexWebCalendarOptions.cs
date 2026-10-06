namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Провайдер календаря, реализующий <c>ICalDavOperations</c>.
/// </summary>
public enum YandexCalendarProvider
{
    /// <summary>
    ///     Веб-API Яндекс.Календаря (calendar.yandex.ru/api/models) от имени пользователя по cookies.
    ///     Перенос реверс-инжиниринга из musicscheduler/yandex_api.py.
    /// </summary>
    Web,

    /// <summary>
    ///     Честный CalDAV (caldav.yandex.ru) по логину/паролю приложения сервисного аккаунта.
    /// </summary>
    CalDav
}

/// <summary>
///     Опции интеграции с Яндекс.Календарём через веб-API (cookies + Playwright).
/// </summary>
public class YandexWebCalendarOptions
{
    public const string SectionName = "YandexCalendar";

    /// <summary>
    ///     Какую реализацию <c>ICalDavOperations</c> регистрировать.
    /// </summary>
    public YandexCalendarProvider Provider { get; set; } = YandexCalendarProvider.Web;

    /// <summary>
    ///     ID слоя (календаря) в Яндекс.Календаре, куда бот пишет репетиции.
    ///     Виден в URL при редактировании календаря (layerId=...).
    /// </summary>
    public long? LayerId { get; set; }

    /// <summary>
    ///     Отображаемое имя календаря (должно содержать «MusicClub» или «Репетиции» — по нему ищет CalendarSyncService).
    /// </summary>
    public string LayerName { get; set; } = "MusicClub — Репетиции";

    /// <summary>
    ///     Часовой пояс, в котором отправляются события.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Moscow";

    /// <summary>
    ///     Путь к файлу с cookies (формат заголовка Cookie: <c>a=1; b=2</c>).
    /// </summary>
    public string CookiePath { get; set; } = Path.Combine("data", "yandex", "cookie.txt");

    /// <summary>
    ///     Директория persistent-профиля Chromium (Playwright). Должна переживать рестарты контейнера.
    /// </summary>
    public string ProfileDir { get; set; } = Path.Combine("data", "yandex", "profile");

    /// <summary>
    ///     Начальные cookies (строка из DevTools). Используются, только если файла <see cref="CookiePath" /> ещё нет.
    /// </summary>
    public string? Cookie { get; set; }

    /// <summary>
    ///     Логин Яндекса для автоматического входа через Playwright, когда сессия протухла.
    /// </summary>
    public string? Login { get; set; }

    /// <summary>
    ///     Пароль Яндекса для автоматического входа через Playwright.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     Разрешить открыть окно браузера для ручного входа (только локально, в контейнере нет дисплея).
    /// </summary>
    public bool AllowInteractiveLogin { get; set; }

    /// <summary>
    ///     Как часто фоново прогонять Playwright, чтобы Яндекс ротировал Session_id. 0 — отключить.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    ///     Минимальный интервал между обновлениями cookies, вызванными ошибками API (защита от шторма).
    /// </summary>
    public TimeSpan MinRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
}
